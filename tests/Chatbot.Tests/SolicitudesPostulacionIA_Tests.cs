using Application.Chats;
using Application.Herramientas;
using Xunit;

namespace Chatbot.Tests;

public sealed class SolicitudesPostulacionIA_Tests
{
    [Fact]
    public void ChatService_NoRecibeClienteQuePuedaIniciarPostulaciones()
    {
        var parametros = typeof(ChatService).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(parametros, p => p.ParameterType.Name.Contains("Postulaciones"));
        // El chat conserva herramientas de consulta de ofertas, no de envio.
        Assert.DoesNotContain(typeof(HerramientasEmpleos).GetMethods(),
            m => m.Name.Contains("Postular", StringComparison.OrdinalIgnoreCase));
    }
}