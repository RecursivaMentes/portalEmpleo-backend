using System.Net.Http.Headers;
using Application.Postulaciones;
using Microsoft.Extensions.Configuration;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class CopiaHistoricaCvClient(HttpClient http, IConfiguration config) : ICopiaHistoricaCv
{
    private const int MaxDocumento = 10 * 1024 * 1024;

    public async Task<CopiaHistoricaCv> ObtenerAsync(Guid usuarioId, Guid cvId, CancellationToken ct = default)
    {
        var clave = config["InternalServices:EmpleosYpostulaciones:Key"];
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("No hay autenticacion interna para consultar el CV.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/internal/usuarios/{usuarioId}/ia/cvs/{cvId}/documento");
        request.Headers.Add("X-Internal-Service", "EmpleosYpostulaciones");
        request.Headers.Add("X-Internal-Key", clave);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxDocumento)
            throw new InvalidOperationException("El CV supera el limite para adjuntarlo.");
        await using var contenido = await response.Content.ReadAsStreamAsync(ct);
        using var memoria = new MemoryStream();
        var buffer = new byte[65536];
        int n;
        while ((n = await contenido.ReadAsync(buffer, ct)) > 0)
        {
            if (memoria.Length + n > MaxDocumento)
                throw new InvalidOperationException("El CV supera el limite para adjuntarlo.");
            await memoria.WriteAsync(buffer.AsMemory(0, n), ct);
        }
        if (memoria.Length == 0) throw new InvalidOperationException("El CV no tiene documento utilizable.");
        var tipo = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        var nombre = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "cv-enviado.pdf";
        return new CopiaHistoricaCv(memoria.ToArray(), tipo, Path.GetFileName(nombre));
    }
}