using Application.Empleos;
using Domain.Enums;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class EmpleosEndpoints
{
    public static void MapEmpleosEndpoints(this WebApplication app)
    {
        app.MapGet("/api/empleos", async (
            EmpleoService servicio,
            string? busqueda,
            string? ubicacion,
            ModalidadTrabajo? modalidad,
            TipoContrato? tipoContrato,
            decimal? salarioMin,
            string? orden,
            CancellationToken cancellationToken
        ) =>
        {
            var filtros = new BuscarEmpleosFiltro(
                busqueda,
                ubicacion,
                modalidad,
                tipoContrato,
                salarioMin,
                orden
            );

            var empleos = await servicio.BuscarEmpleosAsync(
                filtros,
                cancellationToken);

            return Results.Ok(empleos);
        });
    }
}
