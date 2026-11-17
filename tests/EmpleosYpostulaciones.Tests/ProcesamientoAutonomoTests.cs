using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class ProcesamientoAutonomoTests
{
    [Theory]
    [InlineData("Automatica", true)]
    [InlineData("Asistida", true)]
    [InlineData("Desactivada", false)]
    [InlineData(null, false)]
    [InlineData("invalida", false)]
    public void SoloModoAutonomoPuedeIniciar(string? modo, bool esperado) =>
        Assert.Equal(esperado, ProcesamientoAutonomoService.Permitido(modo));

    [Fact]
    public void VentanaDiariaEsUtcYNoDependeDelRelojLocal()
    {
        var (inicio, fin) = ProcesamientoAutonomoService.DiaUtc(
            new DateTime(2026, 11, 12, 22, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 11, 12), inicio);
        Assert.Equal(inicio.AddDays(1), fin);
    }

    [Theory]
    [InlineData("nocturno", true)]
    [InlineData("Remoto", true)]
    [InlineData("contrato_inexistente", false)]
    public void ExclusionesSeRespetan(string exclusion, bool excluida)
    {
        var oferta = new Empleo
        {
            Titulo = "Desarrollador", Descripcion = "Turno nocturno",
            Modalidad = ModalidadTrabajo.Remoto, TipoContrato = TipoContrato.TiempoCompleto
        };
        Assert.Equal(excluida, ProcesamientoAutonomoService.Excluida(oferta, [exclusion]));
    }

    [Fact]
    public void SinExclusionesNoBloquea()
    {
        Assert.False(ProcesamientoAutonomoService.Excluida(new Empleo { Titulo = "Ejemplo" }, null));
    }
}
