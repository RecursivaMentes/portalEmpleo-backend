using System.Net;
using System.Net.Http.Json;
using Application.Postulaciones;
using Microsoft.Extensions.Configuration;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class ConsultaCvComparacionIAClient(HttpClient http, IConfiguration conf)
    : IConsultaCvParaCompararIA
{
    public async Task<CvParaCompararIA?> ObtenerAsync(Guid usuarioId, Guid cvId,
        CancellationToken ct = default)
    {
        var key = conf["InternalServices:EmpleosYpostulaciones:Key"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Sin credencial interna.");
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"/internal/usuarios/{usuarioId}/ia/cvs/{cvId}");
        req.Headers.Add("X-Internal-Service", "EmpleosYpostulaciones");
        req.Headers.Add("X-Internal-Key", key);
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CvParaCompararIA>(cancellationToken: ct);
    }
}