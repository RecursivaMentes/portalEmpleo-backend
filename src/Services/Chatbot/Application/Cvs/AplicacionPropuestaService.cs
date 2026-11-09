using Application.Perfil;

namespace Application.Cvs;

public record AplicarPropuestaRequest(
    string? Resumen = null,
    string? Telefono = null,
    List<ExperienciaUsuario>? Experiencias = null,
    List<FormacionUsuario>? Formacion = null,
    List<HabilidadUsuario>? Habilidades = null,
    List<IdiomaUsuario>? Idiomas = null,
    List<ItemIncompleto>? Incompletos = null);

public enum MotivoErrorAplicacion
{
    YaEnCurso,
    PeticionInvalida,
    PerfilNoDisponible,
    NoSePudoGuardar,
}

public record ResumenAplicacion(
    Conteos Agregados,
    int OmitidosPorDuplicado,
    bool ResumenGuardado,
    bool TelefonoGuardado,
    IReadOnlyList<Faltante> Faltantes,
    IReadOnlyList<string> Avisos);

public record ResultadoAplicacion(
    ResumenAplicacion? Resumen, MotivoErrorAplicacion? Motivo = null, string? Mensaje = null)
{
    public bool Exito => Resumen is not null;
}

/// <summary>
/// Guarda en el perfil lo que la persona confirmó de la propuesta (RF-09) y devuelve las preguntas por los datos
/// que todavía faltan (RF-10), para que el cliente o el chat se las haga.
/// </summary>
public class AplicacionPropuestaService(IPerfilUsuarioClient perfiles, GeneracionesEnCurso enCurso)
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

            // Se relee el perfil: los datos que faltan se calculan sobre lo que realmente quedó guardado.
            var final = await perfiles.ObtenerAsync(accessToken, cancellationToken);
            var faltantes = DetectorFaltantes.Detectar(
                final?.Laboral ?? fusion.Perfil,
                final is not null ? final.Telefono : perfil.Telefono ?? fusion.TelefonoNuevo,
                (request.Incompletos ?? []).Where(i => i is not null).Take(MaxIncompletos).ToList());

            return new ResultadoAplicacion(new ResumenAplicacion(
                fusion.Agregados, fusion.OmitidosPorDuplicado, fusion.ResumenAplicado, telefonoGuardado,
                faltantes, avisos));
        }
        finally
        {
            enCurso.Salir(usuarioId);
        }
    }

    private static ResultadoAplicacion Error(MotivoErrorAplicacion motivo, string mensaje) => new(null, motivo, mensaje);
}
