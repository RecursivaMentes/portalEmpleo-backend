using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Application.Auth;
using Application.PerfilLaboral.Dtos;
using Application.PerfilLaboral.Requests;
using Application.PerfilLaboral.Responses;
using PerfilLaboralEntity = Domain.Entities.PerfilLaboral;

namespace Application.PerfilLaboral;

public class PerfilLaboralService(
    IPerfilLaboralRepository perfiles,
    IUsuarioRepository usuarios)
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private static readonly string[] NivelesEducativos =
    [
        "Primaria", "Secundaria", "Terciaria", "Universitaria de grado",
        "Posgrado", "Curso o certificación"
    ];

    private static readonly string[] EstadosFormacion =
        ["EnCurso", "Finalizado", "Incompleto"];

    private static readonly string[] NivelesHabilidad =
        ["Basico", "Intermedio", "Avanzado"];

    private static readonly string[] NivelesIdioma =
        ["A1", "A2", "B1", "B2", "C1", "C2", "Nativo"];

    public async Task<PerfilLaboralResponse?> ObtenerAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Estado)
            return null;

        var perfil = await perfiles.ObtenerPorUsuarioIdAsync(usuarioId, cancellationToken);
        return perfil is null ? null : ConstruirRespuesta(perfil);
    }

    public async Task<(PerfilLaboralResponse Perfil, bool Creado)?> GuardarAsync(
        Guid usuarioId,
        GuardarPerfilLaboralRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Estado)
            return null;

        var normalizado = ValidarRequest(request);

        var existente = await perfiles.ObtenerPorUsuarioIdAsync(usuarioId, cancellationToken);
        var creado = existente is null;
        var perfil = existente ?? new PerfilLaboralEntity
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId
        };

        perfil.Resumen = normalizado.Resumen;
        perfil.ExperienciasJson = JsonSerializer.Serialize(normalizado.Experiencias, OpcionesJson);
        perfil.FormacionJson = JsonSerializer.Serialize(normalizado.Formacion, OpcionesJson);
        perfil.HabilidadesJson = JsonSerializer.Serialize(normalizado.Habilidades, OpcionesJson);
        perfil.IdiomasJson = JsonSerializer.Serialize(normalizado.Idiomas, OpcionesJson);
        perfil.FechaActualizacion = DateTime.UtcNow;

        if (creado)
            await perfiles.AgregarAsync(perfil, cancellationToken);

        await perfiles.GuardarCambiosAsync(cancellationToken);
        return (ConstruirRespuesta(perfil), creado);
    }

    private static PerfilLaboralResponse ConstruirRespuesta(PerfilLaboralEntity perfil)
    {
        var experiencias = Deserializar<ExperienciaLaboralDto>(perfil.ExperienciasJson);
        return new PerfilLaboralResponse(
            perfil.Id,
            perfil.Resumen,
            experiencias.Count == 0 ? null : experiencias,
            experiencias.Count == 0,
            Deserializar<FormacionAcademicaDto>(perfil.FormacionJson),
            Deserializar<HabilidadDto>(perfil.HabilidadesJson),
            Deserializar<IdiomaDto>(perfil.IdiomasJson),
            perfil.FechaActualizacion);
    }

    private static List<T> Deserializar<T>(string json)
    {
        return JsonSerializer.Deserialize<List<T>>(json, OpcionesJson)
            ?? throw new JsonException("Los datos guardados del perfil laboral no tienen el formato esperado.");
    }

    private static GuardarPerfilLaboralRequest ValidarRequest(GuardarPerfilLaboralRequest request)
    {
        if (request is null)
            throw new ValidationException("No recibimos la información de tu perfil laboral. Volvé a intentarlo.");

        var resumen = request.Resumen?.Trim();
        var experiencias = request.Experiencias ?? [];
        var formacion = request.Formacion ?? [];
        var habilidades = request.Habilidades ?? [];
        var idiomas = request.Idiomas ?? [];

        if (resumen?.Length > 2000)
            throw new ValidationException("El resumen no puede superar los 2000 caracteres.");

        if (request.SinExperiencia && experiencias.Count > 0)
            throw new ValidationException("Marcaste que no tenés experiencia laboral, pero también cargaste experiencias. Eliminá esas experiencias o cambiá tu respuesta.");

        if (!request.SinExperiencia && experiencias.Count == 0)
            throw new ValidationException("Agregá al menos una experiencia laboral o indicá que todavía no tenés experiencia.");

        // Declarar que no se tiene experiencia es información suficiente para iniciar el perfil.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var minimo = hoy.AddYears(-100);

        return request with
        {
            Resumen = resumen,
            Experiencias = ValidarExperiencias(experiencias, minimo, hoy),
            Formacion = ValidarFormaciones(formacion, minimo, hoy),
            Habilidades = ValidarHabilidades(habilidades),
            Idiomas = ValidarIdiomas(idiomas)
        };
    }

    private static List<ExperienciaLaboralDto> ValidarExperiencias(
        List<ExperienciaLaboralDto> experiencias, DateOnly minimo, DateOnly hoy)
    {
        var resultado = new List<ExperienciaLaboralDto>(experiencias.Count);
        for (var i = 0; i < experiencias.Count; i++)
        {
            var item = experiencias[i];
            var posicion = i + 1;
            if (item is null)
                throw new ValidationException($"La experiencia laboral {posicion} no tiene información. Completala o eliminá esa entrada.");

            var empresa = item.Empresa?.Trim();
            var puesto = item.Puesto?.Trim();
            var ubicacion = item.Ubicacion?.Trim();
            var descripcion = item.Descripcion?.Trim();

            if (string.IsNullOrWhiteSpace(empresa))
                throw new ValidationException($"Ingresá el nombre de la empresa en la experiencia laboral {posicion}.");
            if (empresa.Length > 150)
                throw new ValidationException($"El nombre de la empresa en la experiencia laboral {posicion} no puede superar los 150 caracteres.");

            if (string.IsNullOrWhiteSpace(puesto))
                throw new ValidationException($"Ingresá el puesto que ocupaste en la experiencia laboral {posicion}.");
            if (puesto.Length > 120)
                throw new ValidationException($"El puesto de la experiencia laboral {posicion} no puede superar los 120 caracteres.");

            if (ubicacion?.Length > 120)
                throw new ValidationException($"La ubicación de la experiencia laboral {posicion} no puede superar los 120 caracteres.");
            if (descripcion?.Length > 2000)
                throw new ValidationException($"La descripción de la experiencia laboral {posicion} no puede superar los 2000 caracteres.");

            if (item.FechaInicio == DateOnly.MinValue)
                throw new ValidationException($"Ingresá la fecha de inicio de la experiencia laboral {posicion}.");
            if (item.FechaInicio > hoy)
                throw new ValidationException($"La fecha de inicio de la experiencia laboral {posicion} no puede ser posterior a hoy.");
            if (item.FechaInicio < minimo)
                throw new ValidationException($"La fecha de inicio de la experiencia laboral {posicion} no puede tener más de 100 años de antigüedad.");

            if (item.FechaFin is { } fin)
            {
                if (fin > hoy)
                    throw new ValidationException($"La fecha de finalización de la experiencia laboral {posicion} no puede ser posterior a hoy.");
                if (fin < minimo)
                    throw new ValidationException($"La fecha de finalización de la experiencia laboral {posicion} no puede tener más de 100 años de antigüedad.");
                if (fin < item.FechaInicio)
                    throw new ValidationException($"La fecha de finalización de la experiencia laboral {posicion} no puede ser anterior a la fecha de inicio.");
            }

            if (item.EsActual && item.FechaFin is not null)
                throw new ValidationException($"Si todavía trabajás en la experiencia laboral {posicion}, dejá sin completar la fecha de finalización.");

            resultado.Add(item with
            {
                Empresa = empresa, Puesto = puesto, Ubicacion = ubicacion, Descripcion = descripcion
            });
        }
        return resultado;
    }

    private static List<FormacionAcademicaDto> ValidarFormaciones(
        List<FormacionAcademicaDto> formaciones, DateOnly minimo, DateOnly hoy)
    {
        var resultado = new List<FormacionAcademicaDto>(formaciones.Count);
        for (var i = 0; i < formaciones.Count; i++)
        {
            var item = formaciones[i];
            var posicion = i + 1;
            if (item is null)
                throw new ValidationException($"La formación académica {posicion} no tiene información. Completala o eliminá esa entrada.");

            var institucion = item.Institucion?.Trim();
            var titulo = item.Titulo?.Trim();
            var descripcion = item.Descripcion?.Trim();
            var nivel = NivelesEducativos.FirstOrDefault(v =>
                string.Equals(v, item.NivelEducativo?.Trim(), StringComparison.OrdinalIgnoreCase));
            var estado = EstadosFormacion.FirstOrDefault(v =>
                string.Equals(v, item.Estado?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(institucion))
                throw new ValidationException($"Ingresá el nombre de la institución en la formación académica {posicion}.");
            if (institucion.Length > 150)
                throw new ValidationException($"El nombre de la institución en la formación académica {posicion} no puede superar los 150 caracteres.");
            if (string.IsNullOrWhiteSpace(item.NivelEducativo))
                throw new ValidationException($"Seleccioná el nivel educativo de la formación académica {posicion}.");
            if (nivel is null)
                throw new ValidationException($"Seleccioná un nivel educativo válido para la formación académica {posicion}.");
            if (string.IsNullOrWhiteSpace(item.Estado))
                throw new ValidationException($"Indicá el estado de la formación académica {posicion}.");
            if (estado is null)
                throw new ValidationException($"Para la formación académica {posicion}, seleccioná En curso, Finalizado o Incompleto.");

            if (nivel is not ("Primaria" or "Secundaria") && string.IsNullOrWhiteSpace(titulo))
                throw new ValidationException($"Ingresá el título correspondiente a la formación académica {posicion}.");
            if (titulo?.Length > 150)
                throw new ValidationException($"El título de la formación académica {posicion} no puede superar los 150 caracteres.");
            if (descripcion?.Length > 2000)
                throw new ValidationException($"La descripción de la formación académica {posicion} no puede superar los 2000 caracteres.");

            if (estado == "Finalizado" && item.FechaFin is null)
                throw new ValidationException($"Indicá la fecha de finalización de la formación académica {posicion}, ya que la marcaste como finalizada.");

            if (item.FechaInicio is { } inicio)
            {
                if (inicio > hoy)
                    throw new ValidationException($"La fecha de inicio de la formación académica {posicion} no puede ser posterior a hoy.");
                if (inicio < minimo)
                    throw new ValidationException($"La fecha de inicio de la formación académica {posicion} no puede tener más de 100 años de antigüedad.");
            }
            if (item.FechaFin is { } fin)
            {
                if (fin > hoy)
                    throw new ValidationException($"La fecha de finalización de la formación académica {posicion} no puede ser posterior a hoy.");
                if (fin < minimo)
                    throw new ValidationException($"La fecha de finalización de la formación académica {posicion} no puede tener más de 100 años de antigüedad.");
                if (item.FechaInicio is { } fechaInicio && fin < fechaInicio)
                    throw new ValidationException($"La fecha de finalización de la formación académica {posicion} no puede ser anterior a la fecha de inicio.");
            }

            resultado.Add(item with
            {
                Institucion = institucion, Titulo = titulo, Descripcion = descripcion,
                NivelEducativo = nivel, Estado = estado
            });
        }
        return resultado;
    }

    private static List<HabilidadDto> ValidarHabilidades(List<HabilidadDto> habilidades)
    {
        var resultado = new List<HabilidadDto>(habilidades.Count);
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < habilidades.Count; i++)
        {
            var item = habilidades[i];
            var posicion = i + 1;
            if (item is null)
                throw new ValidationException($"La habilidad {posicion} no tiene información. Completala o eliminá esa entrada.");

            var nombre = item.Nombre?.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ValidationException($"Ingresá el nombre de la habilidad {posicion}.");
            if (nombre.Length > 100)
                throw new ValidationException($"El nombre de la habilidad {posicion} no puede superar los 100 caracteres.");
            if (!nombres.Add(nombre))
                throw new ValidationException($"La habilidad «{nombre}» ya está incluida en tu perfil.");

            string? nivel = null;
            if (item.Nivel is not null)
            {
                nivel = NivelesHabilidad.FirstOrDefault(v =>
                    string.Equals(v, item.Nivel.Trim(), StringComparison.OrdinalIgnoreCase));
                if (nivel is null)
                    throw new ValidationException($"Para la habilidad «{nombre}», seleccioná Básico, Intermedio o Avanzado.");
            }
            resultado.Add(item with { Nombre = nombre, Nivel = nivel });
        }
        return resultado;
    }

    private static List<IdiomaDto> ValidarIdiomas(List<IdiomaDto> idiomas)
    {
        var resultado = new List<IdiomaDto>(idiomas.Count);
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < idiomas.Count; i++)
        {
            var item = idiomas[i];
            var posicion = i + 1;
            if (item is null)
                throw new ValidationException($"El idioma {posicion} no tiene información. Completalo o eliminá esa entrada.");

            var idioma = item.Idioma?.Trim();
            if (string.IsNullOrWhiteSpace(idioma))
                throw new ValidationException($"Ingresá el nombre del idioma {posicion}.");
            if (idioma.Length > 100)
                throw new ValidationException($"El nombre del idioma {posicion} no puede superar los 100 caracteres.");
            if (!nombres.Add(idioma))
                throw new ValidationException($"El idioma «{idioma}» ya está incluido en tu perfil.");

            if (string.IsNullOrWhiteSpace(item.Nivel))
                throw new ValidationException($"Seleccioná el nivel del idioma «{idioma}».");
            var nivel = NivelesIdioma.FirstOrDefault(v =>
                string.Equals(v, item.Nivel.Trim(), StringComparison.OrdinalIgnoreCase));
            if (nivel is null)
                throw new ValidationException($"Para el idioma «{idioma}», seleccioná A1, A2, B1, B2, C1, C2 o Nativo.");

            resultado.Add(item with { Idioma = idioma, Nivel = nivel });
        }
        return resultado;
    }
}
