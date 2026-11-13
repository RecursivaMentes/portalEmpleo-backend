using System.Text.Json;

namespace Application.Herramientas;

// El contrato HTTP se implementa en Infrastructure; Application no transporta tokens ni URLs.
public sealed record ResultadoSolicitudPostulacionIA(bool Exito, string Estado, string? Mensaje);
public interface IPostulacionesUsuarioClient
{
    Task<ResultadoSolicitudPostulacionIA> SolicitarAsync(string accessToken,
        Guid empleoId, CancellationToken ct = default);
}

public sealed class HerramientasPostulaciones(IPostulacionesUsuarioClient cliente, string jwt)
    : IEjecutorHerramientas
{
    public IReadOnlyList<DefinicionHerramienta> Definiciones { get; } =
    [
        new("solicitar_postulacion_ia",
            "Inicia o recupera la preparaciÃ³n de una oferta interna exclusivamente cuando la persona pidiÃ³ expresamente postularse. No modifica ofertas ni autoriza un CV.",
            """{"type":"object","properties":{"empleo_id":{"type":"string","description":"Id exacto de la oferta."}},"required":["empleo_id"]}""")
    ];

    public async Task<ResultadoHerramienta> EjecutarAsync(LlamadaHerramienta llamada, CancellationToken ct)
    {
        if (llamada.Nombre != "solicitar_postulacion_ia")
            return new ResultadoHerramienta("Herramienta no disponible.", true);
        try
        {
            using var args = JsonDocument.Parse(llamada.ArgumentosJson);
            if (!args.RootElement.TryGetProperty("empleo_id", out var valor) ||
                valor.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(valor.GetString(), out var empleoId) || empleoId == Guid.Empty)
                return new ResultadoHerramienta("Identificador de oferta invÃ¡lido.", true);
            var resultado = await cliente.SolicitarAsync(jwt, empleoId, ct);
            return resultado.Exito
                ? new ResultadoHerramienta($"Solicitud registrada. Estado: {resultado.Estado}. Si requiere aprobaciÃ³n del CV o del envÃ­o, debe realizarla el usuario.", false)
                : new ResultadoHerramienta(resultado.Mensaje ?? "No se pudo registrar la solicitud de postulaciÃ³n.", true);
        }
        catch (JsonException)
        {
            return new ResultadoHerramienta("Los argumentos de la solicitud son invÃ¡lidos.", true);
        }
    }
}