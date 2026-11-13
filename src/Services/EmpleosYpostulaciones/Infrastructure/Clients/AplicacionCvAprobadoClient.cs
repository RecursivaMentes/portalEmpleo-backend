using System.Net;
using System.Net.Http.Json;
using Application.Postulaciones;
using Microsoft.Extensions.Configuration;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class AplicacionCvAprobadoClient(HttpClient http, IConfiguration config)
    : IAplicacionCvAprobado
{
    public async Task<CvAplicado> AplicarAsync(Guid usuarioId, Guid? cvId, bool crearNuevo,
        string? titulo, string texto, long? versionOriginal, CancellationToken ct = default)
    {
        var clave = config["InternalServices:EmpleosYpostulaciones:Key"];
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("No hay credencial de servicio para aplicar CV aprobado.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/internal/usuarios/{usuarioId}/ia/cvs/aplicar-propuesta-aprobada");
        request.Headers.Add("X-Internal-Service", "EmpleosYpostulaciones");
        request.Headers.Add("X-Internal-Key", clave);
        request.Content = JsonContent.Create(new { crearNuevo, cvId, version = versionOriginal,
            titulo, texto });
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw PostulacionException.PreparacionDesactualizada();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CvAplicado>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Usuarios devolvio un CV vacio.");
    }
}