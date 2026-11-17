using Application.Postulaciones;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class ModosEnvioIATests
{
    [Theory]
    [InlineData("Automatica", true)]
    [InlineData("Asistida", false)]
    [InlineData("Desactivada", false)]
    [InlineData(null, false)]
    [InlineData("automatica", false)]
    public void SoloModoAutomaticoPuedeEnviarSinAprobacion(string? modo, bool esperado)
    {
        Assert.Equal(esperado, ProcesamientoAutonomoService.EnvioSinAprobacionPermitido(modo));
    }
}