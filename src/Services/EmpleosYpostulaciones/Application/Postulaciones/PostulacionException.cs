namespace Application.Postulaciones;

/// <summary>Errores de negocio de las postulaciones. La conversion a HTTP corresponde a Api.</summary>
public sealed class PostulacionException : Exception
{
    public string CodigoError { get; }

    private PostulacionException(string codigoError, string mensaje, Exception? innerException = null)
        : base(mensaje, innerException)
    {
        CodigoError = codigoError;
    }

    public static PostulacionException DatosInvalidos() =>
        new("DATOS_INVALIDOS", "UsuarioId, EmpleoId y CvId son obligatorios.");

    public static PostulacionException PresentacionInvalida() =>
        new("PRESENTACION_INVALIDA", "La presentación no puede superar los 4000 caracteres.");

    public static PostulacionException OfertaNoEncontrada() =>
        new("OFERTA_NO_ENCONTRADA", "La oferta laboral no existe.");

    public static PostulacionException OfertaNoDisponible() =>
        new("OFERTA_NO_DISPONIBLE", "La oferta laboral no está publicada.");

    public static PostulacionException OfertaFinalizada() =>
        new("OFERTA_FINALIZADA", "El plazo de postulación de esta oferta finalizó.");

    public static PostulacionException PerfilIncompleto(IReadOnlyList<string>? faltantes) =>
        new("PERFIL_INCOMPLETO", faltantes is { Count: > 0 }
            ? $"Tu perfil laboral está incompleto. Faltan: {string.Join(", ", faltantes)}."
            : "Tu perfil laboral está incompleto. Completá los datos obligatorios.");

    public static PostulacionException CvNoEncontrado() =>
        new("CV_NO_ENCONTRADO", "El CV no existe o no pertenece al usuario.");

    public static PostulacionException Duplicada(Exception? innerException = null) =>
        new("POSTULACION_DUPLICADA", "Ya te postulaste a esta oferta laboral.", innerException);
}
