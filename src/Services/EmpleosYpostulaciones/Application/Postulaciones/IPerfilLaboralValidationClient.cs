namespace Application.Postulaciones;

public interface IPerfilLaboralValidationClient
{
    Task<ValidacionPerfilLaboralResultado> ValidarAsync(
        string token, CancellationToken cancellationToken = default);
}

public record ValidacionPerfilLaboralResultado(bool PerfilCompleto, IReadOnlyList<string> Faltantes);
