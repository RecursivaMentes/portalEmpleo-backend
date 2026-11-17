using System.ComponentModel.DataAnnotations;
using Application.PerfilLaboral.Responses;
using Domain.Entities;

namespace Application.PerfilLaboral;

/// <summary>Regla de completitud para postularse. No modifica la posibilidad de guardar un perfil parcial.</summary>
public static class PerfilCompletitud
{
    public static ValidacionPerfilLaboralResponse Evaluar(Usuario usuario, PerfilLaboralResponse? perfil)
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(usuario.Nombre)) faltantes.Add("Nombre");
        if (string.IsNullOrWhiteSpace(usuario.Apellido)) faltantes.Add("Apellido");
        if (string.IsNullOrWhiteSpace(usuario.Email) || !new EmailAddressAttribute().IsValid(usuario.Email))
            faltantes.Add("Correo electrónico");

        if (perfil is null)
        {
            // El resumen profesional es opcional para postular.
            faltantes.Add("Experiencia laboral o declaración SinExperiencia");
            faltantes.Add("Habilidades");
        }
        else
        {
            // El resumen profesional no bloquea postulaciones.
            // SinExperiencia ya se exige expresamente al guardar el perfil laboral; se refleja en la respuesta existente.
            if (perfil.Experiencias is not { Count: > 0 } && !perfil.SinExperiencia)
                faltantes.Add("Experiencia laboral o declaración SinExperiencia");
            if (perfil.Habilidades is null || perfil.Habilidades.Count == 0)
                faltantes.Add("Habilidades");
        }

        return new ValidacionPerfilLaboralResponse(faltantes.Count == 0, faltantes);
    }
}
