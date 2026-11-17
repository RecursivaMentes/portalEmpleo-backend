namespace Application.Postulaciones;

public sealed record PreferenciasLaborales(
    List<string>? PuestosInteres,
    List<string>? UbicacionesLaborales,
    List<string>? ModalidadesTrabajo,
    List<string>? TiposContrato,
    decimal? ExpectativaSalarial,
    List<string>? Exclusiones)
{
    public static PreferenciasLaborales Vacias() => new([], [], [], [], null, []);
}

public sealed record ConfiguracionPostulaciones(
    string? Modo,
    int CompatibilidadMinima,
    int LimiteDiario,
    PreferenciasLaborales? Preferencias,
    string? OrdenPendientes = null, string ZonaHoraria = "America/Argentina/Buenos_Aires")
{
    // El usuario no activa el procesamiento autonomo por omision.
    public static ConfiguracionPostulaciones Predeterminada() =>
        new("Desactivada", 60, 5, PreferenciasLaborales.Vacias(), "Prioridad", "America/Argentina/Buenos_Aires");
}
