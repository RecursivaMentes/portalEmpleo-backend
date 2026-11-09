using Application.Chats;
using Application.Perfil;

namespace Application.Cvs;

public enum MotivoErrorAnalisis
{
    YaEnCurso,
    CvNoEncontrado,
    CvSinTexto,
    ServicioNoDisponible,
    IaNoFiable,
}

public record ResultadoAnalisisCv(
    ResultadoAnalisis? Analisis, MotivoErrorAnalisis? Motivo = null, string? Mensaje = null)
{
    public bool Exito => Analisis is not null;
}

/// <summary>
/// Lee el texto de un CV con IA y propone los datos que se podrían cargar en el perfil (RF-09), e indica qué
/// datos seguirían faltando para tener un perfil completo (RF-10).
///
/// IMPORTANTE: este servicio NUNCA escribe en el perfil. Devuelve una propuesta que la persona revisa y confirma
/// (ver <see cref="AplicacionPropuestaService"/>). Una IA puede equivocarse, y un perfil con datos erróneos
/// afecta a todas las postulaciones y a los CV que se generen después.
/// </summary>
public class AnalisisCvService(
    IAsistenteIA asistente,
    IPerfilUsuarioClient perfiles,
    ICvUsuarioClient cvs,
    GeneracionesEnCurso enCurso)
{
    private const int MaxIntentos = 2;

    /// <exception cref="AsistenteNoDisponibleException">Si el modelo de IA no está disponible.</exception>
    public async Task<ResultadoAnalisisCv> AnalizarAsync(
        Guid usuarioId, string accessToken, Guid cvId, CancellationToken cancellationToken)
    {
        if (!enCurso.TryEntrar(usuarioId))
            return Error(MotivoErrorAnalisis.YaEnCurso, GeneracionesEnCurso.MensajeYaEnCurso);

        try
        {
            var lectura = await cvs.ObtenerAsync(accessToken, cvId, cancellationToken);
            if (lectura.NoEncontrado)
                return Error(MotivoErrorAnalisis.CvNoEncontrado, "No encontramos ese CV.");
            if (!lectura.Exito)
                return Error(MotivoErrorAnalisis.ServicioNoDisponible,
                    "No pudimos consultar tu CV en este momento. Intentá nuevamente más tarde.");

            var textoCv = lectura.Cv!.TextoPlano?.Trim();
            if (string.IsNullOrEmpty(textoCv))
                return Error(MotivoErrorAnalisis.CvSinTexto,
                    "Este CV no tiene texto para analizar. Subí un archivo con texto seleccionable o escribí su contenido.");

            var perfil = await perfiles.ObtenerAsync(accessToken, cancellationToken);
            if (perfil is null)
                return Error(MotivoErrorAnalisis.ServicioNoDisponible,
                    "No pudimos consultar tu perfil en este momento. Intentá nuevamente más tarde.");

            var avisos = new List<string>();
            var recortado = textoCv.Length > PromptExtraccionPerfil.MaxCaracteresCv;
            if (recortado)
                avisos.Add($"El CV es muy largo: se analizaron solo los primeros {PromptExtraccionPerfil.MaxCaracteresCv} caracteres.");

            var textoAnalizado = recortado ? textoCv[..PromptExtraccionPerfil.MaxCaracteresCv] : textoCv;
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

            PropuestaCruda? cruda = null;
            string? problema = null;

            for (var intento = 1; intento <= MaxIntentos && cruda is null; intento++)
            {
                var respuesta = await asistente.GenerarTextoAsync(
                    PromptExtraccionPerfil.Instrucciones,
                    PromptExtraccionPerfil.ConstruirSolicitud(textoAnalizado, problema),
                    cancellationToken,
                    respuestaJson: true);

                cruda = LectorPropuestaIA.Interpretar(respuesta, textoAnalizado, hoy);
                problema = "No era un objeto JSON válido con la forma indicada.";
            }

            if (cruda is null)
                return Error(MotivoErrorAnalisis.IaNoFiable,
                    "La IA no logró interpretar tu CV. Intentá de nuevo.");

            var propuesta = FusionPerfil.Marcar(cruda, perfil);

            // Qué le seguiría faltando al perfil si la persona aceptara todo lo nuevo de la propuesta.
            var simulado = FusionPerfil.Fusionar(perfil, FusionPerfil.SoloNuevos(propuesta));
            var faltantes = DetectorFaltantes.Detectar(
                simulado.Perfil, perfil.Telefono ?? simulado.TelefonoNuevo, cruda.Incompletos);

            if (HayPropuesta(propuesta) is false && cruda.Incompletos.Count == 0)
                avisos.Add("No encontramos datos para importar en este CV.");

            return new ResultadoAnalisisCv(new ResultadoAnalisis(
                cvId, propuesta, cruda.Incompletos, faltantes, cruda.Descartados, avisos));
        }
        finally
        {
            enCurso.Salir(usuarioId);
        }
    }

    private static bool HayPropuesta(PropuestaPerfil p) =>
        p.Resumen is not null || p.Telefono is not null
        || p.Experiencias.Count + p.Formacion.Count + p.Habilidades.Count + p.Idiomas.Count > 0;

    private static ResultadoAnalisisCv Error(MotivoErrorAnalisis motivo, string mensaje) => new(null, motivo, mensaje);
}
