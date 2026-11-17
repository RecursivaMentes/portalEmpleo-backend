using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class AutenticacionInternaTests
{
    [Fact]
    public void ElProcesadorNoIntegradoNoPuedeEnviarPostulaciones()
    {
        var preparador = new EmpleosYpostulaciones.Infrastructure.Services.PreparadorIAEnEspera();
        Assert.False(preparador.Disponible);
    }
}
