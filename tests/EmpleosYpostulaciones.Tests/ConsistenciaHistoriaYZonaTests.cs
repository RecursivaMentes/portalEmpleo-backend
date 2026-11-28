using Application.Postulaciones;
using Domain.Enums;
using Domain.Entities;
using EmpleosYpostulaciones.Infrastructure;
using Microsoft.EntityFrameworkCore;
using EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class ConsistenciaHistoriaYZonaTests
{
    [Fact]
    public void DiaLocal_NoReiniciaAlCambiarElDiaUtc()
    {
        var a = new DateTime(2026, 11, 14, 1, 30, 0, DateTimeKind.Utc);
        var (inicio, fin) = ProcesamientoAutonomoService.DiaLocalUtc(a,
            "America/Argentina/Buenos_Aires");
        Assert.Equal(new DateTime(2026, 11, 13, 3, 0, 0, DateTimeKind.Utc), inicio);
        Assert.Equal(new DateTime(2026, 11, 14, 3, 0, 0, DateTimeKind.Utc), fin);
    }

    [Fact]
    public void RetiradaSoloPuedeHacerlaElPostulante()
    {
        Assert.True(PostulacionRepository.TransicionValida(EstadoPostulacion.EnRevision,
            EstadoPostulacion.Retirada, true));
        Assert.False(PostulacionRepository.TransicionValida(EstadoPostulacion.EnRevision,
            EstadoPostulacion.Retirada, false));
    }
    [Fact]
    public void IndiceUnicoExcluyeRetiradas()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=prueba_modelo;Username=postgres;Password=prueba")
            .Options;
        using var db = new AppDbContext(opciones);
        var indice = db.Model.FindEntityType(typeof(Postulacion))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "UsuarioId", "EmpleoId" }));
        Assert.True(indice.IsUnique);
        Assert.Equal("\"Estado\" <> 'Retirada'", indice.GetFilter());
    }

    [Fact]
    public void NoPermitirVolverDesdeRechazada()
    {
        Assert.False(PostulacionRepository.TransicionValida(EstadoPostulacion.Rechazada,
            EstadoPostulacion.EnRevision, false));
    }
}