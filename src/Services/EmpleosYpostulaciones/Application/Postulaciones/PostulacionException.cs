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

    public static PostulacionException PendienteDuplicado() =>
        new("PENDIENTE_DUPLICADO", "La oferta ya se encuentra en pendientes.");

    public static PostulacionException PreparacionDuplicada() =>
        new("PREPARACION_DUPLICADA", "Ya existe una preparacion pendiente para esta oferta.");

    public static PostulacionException IaOcupada() =>
        new("IA_OCUPADA", "La IA esta trabajando en otra postulacion.");

    public static PostulacionException PreparacionNoCancelable() =>
        new("PREPARACION_NO_CANCELABLE", "La preparacion no puede cancelarse en este estado.");

    public static PostulacionException PreparacionNoEditable() =>
        new("PREPARACION_NO_EDITABLE", "La preparacion no esta pendiente de aprobacion.");
    public static PostulacionException PreparacionDesactualizada() =>
        new("PREPARACION_DESACTUALIZADA", "La preparacion cambio de estado; actualiza antes de enviar.");
    public static PostulacionException CambiosSinAceptar() =>
        new("CAMBIOS_SIN_ACEPTAR", "Hay cambios propuestos pendientes de aceptar o descartar.");
    public static PostulacionException SinCambios() =>
        new("SIN_CAMBIOS", "No hay cambios nuevos para revisar o aceptar.");
    public static PostulacionException SesionNoValida() =>
        new("SESION_NO_VALIDA", "La sesion no permite validar los datos externos.");
    public static PostulacionException OrdenamientoNoDisponible(Exception? ex = null) =>
        new("ORDENAMIENTO_NO_DISPONIBLE", "No se pudo consultar el ordenamiento de postulaciones.", ex);
    public static PostulacionException LimiteDiarioAlcanzado() =>
        new("LIMITE_DIARIO_ALCANZADO", "Se alcanzÃ³ el lÃ­mite diario de preparaciones o envÃ­os autÃ³nomos.");
    public static PostulacionException EstadoInvalido() =>
        new("TRANSICION_ESTADO_INVALIDA", "El cambio de estado solicitado no estÃ¡ permitido.");
    public static PostulacionException DatosExternosInvalidos() =>
        new("DATOS_EXTERNOS_INVALIDOS", "El servicio consultado devolvio datos invalidos.");
}
