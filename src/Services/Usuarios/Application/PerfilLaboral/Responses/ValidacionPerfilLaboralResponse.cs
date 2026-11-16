namespace Application.PerfilLaboral.Responses;

public record ValidacionPerfilLaboralResponse(bool PerfilCompleto, IReadOnlyList<string> Faltantes);
