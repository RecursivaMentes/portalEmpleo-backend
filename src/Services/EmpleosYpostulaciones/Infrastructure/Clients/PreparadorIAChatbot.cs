using System.Net.Http.Json;
using Application.Postulaciones;
using Microsoft.Extensions.Configuration;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class PreparadorIAChatbot(HttpClient http, IConfiguration config) : IPreparadorPostulacionesIA
{
    public bool Disponible => config.GetValue<bool>("PostulacionesIA:IntegracionChatbotHabilitada");

    public async Task<ResultadoPreparacionIA> PrepararAsync(Guid usuarioId,
        Guid empleoId, CancellationToken ct = default)
    {
        if (!Disponible) throw new InvalidOperationException("Integracion Chatbot desactivada.");
        var clave = config["InternalServices:EmpleosYpostulaciones:Key"];
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("Falta credencial interna de EmpleosYpostulaciones.");
        using var solicitud = new HttpRequestMessage(HttpMethod.Post,
            "/internal/chatbot/postulaciones/preparar");
        solicitud.Headers.Add("X-Internal-Service", "EmpleosYpostulaciones");
        solicitud.Headers.Add("X-Internal-Key", clave);
        solicitud.Content = JsonContent.Create(new { usuarioId, empleoId });
        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        var datos = await respuesta.Content.ReadFromJsonAsync<PreparacionRespuesta>(cancellationToken: ct)
            ?? throw new InvalidOperationException("El Chatbot devolvio una preparacion vacia.");
        return new ResultadoPreparacionIA(datos.CvId, datos.TextoPresentacion,
            datos.RequiereAprobacionCv, datos.CvTextoPropuesto, datos.CvTituloPropuesto, datos.CvVersionOriginal, datos.CrearCvNuevo);
    }

    public async Task<ResultadoPreparacionIA> ModificarAsync(Guid usuarioId, Guid empleoId,
        string instrucciones, Guid? cvAnterior, string? textoAnterior, CancellationToken ct = default)
    {
        if (!Disponible) throw new InvalidOperationException("Integracion Chatbot desactivada.");
        var clave = config["InternalServices:EmpleosYpostulaciones:Key"];
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("Falta credencial interna de EmpleosYpostulaciones.");
        using var solicitud = new HttpRequestMessage(HttpMethod.Post,
            "/internal/chatbot/postulaciones/preparar");
        solicitud.Headers.Add("X-Internal-Service", "EmpleosYpostulaciones");
        solicitud.Headers.Add("X-Internal-Key", clave);
        solicitud.Content = JsonContent.Create(new { usuarioId, empleoId,
            instrucciones, cvPreferido = cvAnterior, textoAnterior });
        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        var datos = await respuesta.Content.ReadFromJsonAsync<PreparacionRespuesta>(cancellationToken: ct)
            ?? throw new InvalidOperationException("El Chatbot devolvio una propuesta vacia.");
        return new ResultadoPreparacionIA(datos.CvId, datos.TextoPresentacion,
            datos.RequiereAprobacionCv, datos.CvTextoPropuesto, datos.CvTituloPropuesto,
            datos.CvVersionOriginal, datos.CrearCvNuevo);
    }
    private sealed record PreparacionRespuesta(Guid? CvId, string? TextoPresentacion,
        bool RequiereAprobacionCv, string? CvTextoPropuesto, string? CvTituloPropuesto, long? CvVersionOriginal, bool CrearCvNuevo);
}