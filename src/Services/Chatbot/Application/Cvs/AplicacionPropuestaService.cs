using System.Text;
using System.Text.RegularExpressions;
using Application.Chats;
using Application.Perfil;
using Domain.Entities;
using Domain.Enums;

namespace Application.Cvs;

public record AplicarPropuestaRequest(
    string? Resumen = null,
    string? Telefono = null,
    List<ExperienciaUsuario>? Experiencias = null,
    List<FormacionUsuario>? Formacion = null,
    List<HabilidadUsuario>? Habilidades = null,
    List<IdiomaUsuario>? Idiomas = null,
    List<ItemIncompleto>? Incompletos = null,
    bool IniciarChat = false);

public enum MotivoErrorAplicacion
{
    YaEnCurso,
    PeticionInvalida,
    PerfilNoDisponible,
    NoSePudoGuardar,
}

/// <param name="ChatId">El chat donde el asistente dejó las preguntas, si se pidió y había algo que preguntar.</param>
public record ResumenAplicacion(
    Conteos Agregados,
    int OmitidosPorDuplicado,
    bool ResumenGuardado,
    bool TelefonoGuardado,
    IReadOnlyList<Faltante> Faltantes,
    Guid? ChatId,
    IReadOnlyList<string> Avisos);

public record ResultadoAplicacion(
    ResumenAplicacion? Resumen, MotivoErrorAplicacion? Motivo = null, string? Mensaje = null)
{
    public bool Exito => Resumen is not null;
}

/// <summary>
/// Guarda en el perfil lo que la persona confirmó de la propuesta (RF-09) y, si se pide, deja abierto un chat
/// con las preguntas por los datos que faltan (RF-10), que el asistente va completando con sus herramientas.
/// </summary>
public class AplicacionPropuestaService(
    IPerfilUsuarioClient perfiles,
    IChatRepository chats,
    GeneracionesEnCurso enCurso)
{
    public const int MaxItemsPorLista = 50;
    private const int MaxIncompletos = 10;

    public async Task<ResultadoAplicacion> AplicarAsync(
        Guid usuarioId, string accessToken, AplicarPropuestaRequest request, CancellationToken cancellationToken)
    {
        var datos = new DatosAImportar(
            request.Resumen,
            request.Telefono,
            request.Experiencias ?? [],
            request.Formacion ?? [],
            request.Habilidades ?? [],
            request.Idiomas ?? []);

        if (datos.Experiencias.Count > MaxItemsPorLista || datos.Formacion.Count > MaxItemsPorLista
            || datos.Habilidades.Count > MaxItemsPorLista || datos.Idiomas.Count > MaxItemsPorLista)
            return Error(MotivoErrorAplicacion.PeticionInvalida,
                $"No se pueden importar más de {MaxItemsPorLista} elementos de cada tipo a la vez.");

        if (string.IsNullOrWhiteSpace(datos.Resumen) && string.IsNullOrWhiteSpace(datos.Telefono)
            && datos.Experiencias.Count + datos.Formacion.Count + datos.Habilidades.Count + datos.Idiomas.Count == 0)
            return Error(MotivoErrorAplicacion.PeticionInvalida, "No hay nada para importar.");

        // Guardar el perfil es leer-modificar-escribir (Usuarios lo reemplaza entero): dos a la vez se pisarían.
        if (!enCurso.TryEntrar(usuarioId))
            return Error(MotivoErrorAplicacion.YaEnCurso, GeneracionesEnCurso.MensajeYaEnCurso);

        try
        {
            var perfil = await perfiles.ObtenerAsync(accessToken, cancellationToken);
            if (perfil is null)
                return Error(MotivoErrorAplicacion.PerfilNoDisponible,
                    "No pudimos consultar tu perfil en este momento. Intentá nuevamente más tarde.");

            var fusion = FusionPerfil.Fusionar(perfil, datos);
            var avisos = new List<string>();

            if (fusion.OmitidosPorDuplicado > 0)
                avisos.Add($"{fusion.OmitidosPorDuplicado} elemento(s) ya estaban en tu perfil y no se repitieron.");

            if (fusion.Agregados.Total > 0 || fusion.ResumenAplicado)
            {
                // Usuarios no acepta un perfil sin experiencias salvo que la persona declare que no tiene.
                if (fusion.Perfil.Experiencias.Count == 0 && !fusion.Perfil.SinExperiencia)
                    return Error(MotivoErrorAplicacion.NoSePudoGuardar,
                        "Para guardar tu perfil necesitamos al menos una experiencia laboral, o que indiques que todavía " +
                        "no tenés experiencia. Contanos cuál es tu caso en el chat.");

                var guardado = await perfiles.GuardarPerfilLaboralAsync(accessToken, fusion.Perfil, cancellationToken);
                if (!guardado.Exito)
                    return Error(MotivoErrorAplicacion.NoSePudoGuardar,
                        guardado.Mensaje ?? "No se pudo guardar tu perfil.");
            }

            var telefonoGuardado = false;
            if (fusion.TelefonoNuevo is not null)
            {
                var r = await perfiles.ActualizarDatosPersonalesAsync(
                    accessToken, new ActualizacionDatosPersonales(Telefono: fusion.TelefonoNuevo), cancellationToken);

                telefonoGuardado = r.Exito;
                if (!r.Exito) avisos.Add($"No pudimos guardar el teléfono: {r.Mensaje}");
            }

            // Se vuelve a leer el perfil guardado: los datos que faltan se calculan sobre lo que realmente quedó.
            var final = await perfiles.ObtenerAsync(accessToken, cancellationToken);
            var faltantes = DetectorFaltantes.Detectar(
                final?.Laboral ?? fusion.Perfil,
                final is not null ? final.Telefono : perfil.Telefono ?? fusion.TelefonoNuevo,
                LimpiarIncompletos(request.Incompletos));

            Guid? chatId = null;
            if (request.IniciarChat && faltantes.Count > 0)
                chatId = await AbrirChatConPreguntasAsync(usuarioId, fusion.Agregados, faltantes, cancellationToken);

            return new ResultadoAplicacion(new ResumenAplicacion(
                fusion.Agregados, fusion.OmitidosPorDuplicado, fusion.ResumenAplicado, telefonoGuardado,
                faltantes, chatId, avisos));
        }
        finally
        {
            enCurso.Salir(usuarioId);
        }
    }

    private async Task<Guid> AbrirChatConPreguntasAsync(
        Guid usuarioId, Conteos agregados, IReadOnlyList<Faltante> faltantes, CancellationToken cancellationToken)
    {
        var chat = new Chat { Id = Guid.NewGuid(), UsuarioId = usuarioId, Tipo = TipoChat.Asistente };
        await chats.AddChatAsync(chat, cancellationToken);

        await chats.AddMensajesAsync([new Mensaje
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Rol = RolMensaje.Asistente,
            Contenido = MensajeInicial(agregados, faltantes),
        }], cancellationToken);

        return chat.Id;
    }

    /// <summary>El primer mensaje del chat: qué se importó y qué se necesita para completar el perfil.</summary>
    internal static string MensajeInicial(Conteos agregados, IReadOnlyList<Faltante> faltantes)
    {
        var sb = new StringBuilder();

        var importados = new List<string>();
        if (agregados.Experiencias > 0) importados.Add($"{agregados.Experiencias} experiencia(s) laboral(es)");
        if (agregados.Formacion > 0) importados.Add($"{agregados.Formacion} formación(es)");
        if (agregados.Habilidades > 0) importados.Add($"{agregados.Habilidades} habilidad(es)");
        if (agregados.Idiomas > 0) importados.Add($"{agregados.Idiomas} idioma(s)");

        sb.AppendLine(importados.Count > 0
            ? $"¡Listo! Pasé a tu perfil lo que encontré en tu CV: {string.Join(", ", importados)}."
            : "Revisé tu perfil.");
        sb.AppendLine();
        sb.AppendLine("Para dejarlo más completo, me faltan estos datos:");

        for (var i = 0; i < faltantes.Count; i++)
            sb.AppendLine($"{i + 1}. {faltantes[i].Pregunta}");

        sb.AppendLine();
        sb.Append("Podés contestarme de a uno o todos juntos, y los voy guardando en tu perfil.");

        return sb.ToString();
    }

    // Los pendientes los manda el cliente y terminan dentro de un mensaje del chat: se limitan en cantidad y largo
    // y se quitan saltos de línea y caracteres de control, para que no puedan colar texto con otro formato.
    private static IReadOnlyList<ItemIncompleto> LimpiarIncompletos(List<ItemIncompleto>? incompletos)
    {
        if (incompletos is null) return [];

        var tiposValidos = new[] { "experiencia", "formacion", "idioma" };

        return incompletos
            .Where(i => i is not null && tiposValidos.Contains(i.Tipo) && !string.IsNullOrWhiteSpace(i.Descripcion))
            .Take(MaxIncompletos)
            .Select(i => new ItemIncompleto(
                i.Tipo,
                Limpiar(i.Descripcion, 150),
                (i.Faltan ?? []).Take(5).Select(f => Limpiar(f, 60)).Where(f => f.Length > 0).ToList()))
            .ToList();
    }

    private static string Limpiar(string? texto, int maximo)
    {
        var limpio = Regex.Replace(texto ?? "", @"[\p{C}\s]+", " ").Trim();
        return limpio.Length > maximo ? limpio[..maximo].TrimEnd() : limpio;
    }

    private static ResultadoAplicacion Error(MotivoErrorAplicacion motivo, string mensaje) => new(null, motivo, mensaje);
}
