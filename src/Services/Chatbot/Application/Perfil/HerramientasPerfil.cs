using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Herramientas;

namespace Application.Perfil;

/// <summary>
/// Herramientas que permiten al asistente completar el perfil de la persona.
/// Cada una modifica un solo dato: como Usuarios reemplaza el perfil laboral entero al guardar,
/// el código lee el perfil vigente, aplica el cambio y lo guarda completo. Así el modelo nunca
/// puede borrar datos sin querer. No hay herramientas de borrado.
/// </summary>
public class HerramientasPerfil(IPerfilUsuarioClient perfiles, string accessToken) : IEjecutorHerramientas
{
    // Valores que acepta Usuarios. Si allá cambian, el servicio rechaza el guardado y el modelo recibe el error.
    private static readonly string[] NivelesEducativos =
        ["Primaria", "Secundaria", "Terciaria", "Universitaria de grado", "Posgrado", "Curso o certificación"];

    private static readonly string[] EstadosFormacion = ["EnCurso", "Finalizado", "Incompleto"];
    private static readonly string[] NivelesHabilidad = ["Basico", "Intermedio", "Avanzado"];
    private static readonly string[] NivelesIdioma = ["A1", "A2", "B1", "B2", "C1", "C2", "Nativo"];
    private static readonly string[] Modalidades = ["Presencial", "Remoto", "Hibrido"];

    private const int MaxItemsLista = 20;
    private const int MaxLargoItem = 100;
    private const int MaxLargoResumen = 2000;

    public IReadOnlyList<DefinicionHerramienta> Definiciones { get; } =
    [
        Def("agregar_experiencia",
            "Agrega una experiencia laboral al perfil. Fechas en formato AAAA-MM-DD (si solo sabes mes y año usa el día 01; si falta el año, pregúntalo).",
            new JsonObject
            {
                ["empresa"] = Texto("Nombre de la empresa."),
                ["puesto"] = Texto("Puesto que ocupó."),
                ["ubicacion"] = Texto("Ciudad o país (opcional)."),
                ["descripcion"] = Texto("Tareas o logros (opcional)."),
                ["fecha_inicio"] = Texto("Fecha de inicio, AAAA-MM-DD."),
                ["fecha_fin"] = Texto("Fecha de fin, AAAA-MM-DD. Omitir si es el trabajo actual."),
                ["es_actual"] = Booleano("true si todavía trabaja ahí."),
            },
            "empresa", "puesto", "fecha_inicio", "es_actual"),

        Def("agregar_formacion",
            "Agrega una formación académica (estudio o curso) al perfil. Fechas en formato AAAA-MM-DD.",
            new JsonObject
            {
                ["institucion"] = Texto("Nombre de la institución."),
                ["titulo"] = Texto("Título o nombre del curso."),
                ["nivel_educativo"] = Opciones("Nivel educativo.", NivelesEducativos),
                ["estado"] = Opciones("Estado del estudio.", EstadosFormacion),
                ["fecha_inicio"] = Texto("Fecha de inicio, AAAA-MM-DD (opcional)."),
                ["fecha_fin"] = Texto("Fecha de fin, AAAA-MM-DD. Obligatoria si el estado es Finalizado."),
                ["descripcion"] = Texto("Detalle (opcional)."),
            },
            "institucion", "nivel_educativo", "estado"),

        Def("agregar_habilidad",
            "Agrega una habilidad al perfil. Si ya existe, actualiza su nivel.",
            new JsonObject
            {
                ["nombre"] = Texto("Nombre de la habilidad, por ejemplo C# o Excel."),
                ["nivel"] = Opciones("Nivel (opcional).", NivelesHabilidad),
            },
            "nombre"),

        Def("agregar_idioma",
            "Agrega un idioma al perfil. Si ya existe, actualiza su nivel.",
            new JsonObject
            {
                ["idioma"] = Texto("Nombre del idioma."),
                ["nivel"] = Opciones("Nivel según el marco europeo (A1 a C2) o Nativo.", NivelesIdioma),
            },
            "idioma", "nivel"),

        Def("actualizar_resumen",
            "Reemplaza el resumen profesional del perfil por el texto indicado.",
            new JsonObject { ["resumen"] = Texto("Resumen profesional, máximo 2000 caracteres.") },
            "resumen"),

        Def("indicar_sin_experiencia",
            "Registra que la persona todavía no tiene experiencia laboral. Solo funciona si no tiene experiencias cargadas.",
            new JsonObject()),

        Def("actualizar_datos_personales",
            "Actualiza el nombre, el apellido o el teléfono. Solo se modifican los campos que envíes.",
            new JsonObject
            {
                ["nombre"] = Texto("Nombre (opcional)."),
                ["apellido"] = Texto("Apellido (opcional)."),
                ["telefono"] = Texto("Teléfono (opcional)."),
            }),

        Def("actualizar_preferencias",
            "Actualiza las preferencias laborales. Cada lista que envíes REEMPLAZA a la anterior (envía la lista completa, no solo lo nuevo). Las que no envíes no se tocan.",
            new JsonObject
            {
                ["puestos_interes"] = Lista("Puestos que le interesan."),
                ["ubicaciones"] = Lista("Ubicaciones donde quiere trabajar."),
                ["modalidades"] = ListaOpciones("Modalidades aceptadas.", Modalidades),
                ["excluir"] = Lista("Condiciones o rubros que quiere evitar."),
            }),
    ];

    public async Task<ResultadoHerramienta> EjecutarAsync(LlamadaHerramienta llamada, CancellationToken cancellationToken)
    {
        JsonElement args;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(llamada.ArgumentosJson) ? "{}" : llamada.ArgumentosJson);
            args = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Error("Los argumentos no son un JSON válido.");
        }

        if (args.ValueKind != JsonValueKind.Object)
            return Error("Los argumentos deben ser un objeto JSON.");

        return llamada.Nombre switch
        {
            "agregar_experiencia" => await AgregarExperienciaAsync(args, cancellationToken),
            "agregar_formacion" => await AgregarFormacionAsync(args, cancellationToken),
            "agregar_habilidad" => await AgregarHabilidadAsync(args, cancellationToken),
            "agregar_idioma" => await AgregarIdiomaAsync(args, cancellationToken),
            "actualizar_resumen" => await ActualizarResumenAsync(args, cancellationToken),
            "indicar_sin_experiencia" => await IndicarSinExperienciaAsync(cancellationToken),
            "actualizar_datos_personales" => await ActualizarDatosPersonalesAsync(args, cancellationToken),
            "actualizar_preferencias" => await ActualizarPreferenciasAsync(args, cancellationToken),
            _ => Error($"La herramienta '{llamada.Nombre}' no existe."),
        };
    }

    // ---------- perfil laboral ----------

    private async Task<ResultadoHerramienta> AgregarExperienciaAsync(JsonElement a, CancellationToken ct)
    {
        var empresa = Cadena(a, "empresa");
        var puesto = Cadena(a, "puesto");
        if (empresa is null) return Error("Falta 'empresa'.");
        if (puesto is null) return Error("Falta 'puesto'.");

        if (!TryFecha(a, "fecha_inicio", obligatoria: true, out var inicio, out var error)) return Error(error);
        if (!TryFecha(a, "fecha_fin", obligatoria: false, out var fin, out error)) return Error(error);

        var esActual = Booleano(a, "es_actual");
        if (esActual is null) return Error("Falta 'es_actual' (true o false).");
        if (esActual == true && fin is not null)
            return Error("Si es_actual es true no se debe enviar fecha_fin.");

        var nueva = new ExperienciaUsuario(
            empresa, puesto, Cadena(a, "ubicacion"), Cadena(a, "descripcion"), inicio, fin, esActual.Value);

        return await ModificarLaboralAsync($"Experiencia en {empresa} guardada.", laboral =>
        {
            var repetida = laboral.Experiencias.Any(e =>
                Igual(e.Empresa, nueva.Empresa) && Igual(e.Puesto, nueva.Puesto) && e.FechaInicio == nueva.FechaInicio);

            return repetida
                ? null
                : laboral with { SinExperiencia = false, Experiencias = [.. laboral.Experiencias, nueva] };
        }, ct);
    }

    private async Task<ResultadoHerramienta> AgregarFormacionAsync(JsonElement a, CancellationToken ct)
    {
        var institucion = Cadena(a, "institucion");
        if (institucion is null) return Error("Falta 'institucion'.");

        var nivel = Canonico(Cadena(a, "nivel_educativo"), NivelesEducativos);
        if (nivel is null) return Error($"'nivel_educativo' debe ser uno de: {string.Join(", ", NivelesEducativos)}.");

        var estado = Canonico(Cadena(a, "estado"), EstadosFormacion);
        if (estado is null) return Error($"'estado' debe ser uno de: {string.Join(", ", EstadosFormacion)}.");

        if (!TryFecha(a, "fecha_inicio", obligatoria: false, out var inicio, out var error)) return Error(error);
        if (!TryFecha(a, "fecha_fin", obligatoria: false, out var fin, out error)) return Error(error);

        var nueva = new FormacionUsuario(
            institucion, Cadena(a, "titulo"), nivel, estado, inicio, fin, Cadena(a, "descripcion"));

        return await ModificarLaboralAsync($"Formación en {institucion} guardada.", laboral =>
        {
            var repetida = laboral.Formacion.Any(f =>
                Igual(f.Institucion, nueva.Institucion) && Igual(f.Titulo, nueva.Titulo) && f.NivelEducativo == nueva.NivelEducativo);

            return repetida ? null : laboral with { Formacion = [.. laboral.Formacion, nueva] };
        }, ct);
    }

    private async Task<ResultadoHerramienta> AgregarHabilidadAsync(JsonElement a, CancellationToken ct)
    {
        var nombre = Cadena(a, "nombre");
        if (nombre is null) return Error("Falta 'nombre'.");

        string? nivel = null;
        if (Cadena(a, "nivel") is { } textoNivel)
        {
            nivel = Canonico(textoNivel, NivelesHabilidad);
            if (nivel is null) return Error($"'nivel' debe ser uno de: {string.Join(", ", NivelesHabilidad)}.");
        }

        return await ModificarLaboralAsync($"Habilidad {nombre} guardada.", laboral =>
        {
            var existente = laboral.Habilidades.FirstOrDefault(h => Igual(h.Nombre, nombre));
            if (existente is null)
                return laboral with { Habilidades = [.. laboral.Habilidades, new HabilidadUsuario(nombre, nivel)] };

            // Ya estaba: solo se actualiza el nivel si cambió.
            if (nivel is null || existente.Nivel == nivel) return null;

            return laboral with
            {
                Habilidades = [.. laboral.Habilidades.Select(h => Igual(h.Nombre, nombre) ? h with { Nivel = nivel } : h)]
            };
        }, ct);
    }

    private async Task<ResultadoHerramienta> AgregarIdiomaAsync(JsonElement a, CancellationToken ct)
    {
        var idioma = Cadena(a, "idioma");
        if (idioma is null) return Error("Falta 'idioma'.");

        var nivel = Canonico(Cadena(a, "nivel"), NivelesIdioma);
        if (nivel is null) return Error($"'nivel' debe ser uno de: {string.Join(", ", NivelesIdioma)}.");

        return await ModificarLaboralAsync($"Idioma {idioma} guardado.", laboral =>
        {
            var existente = laboral.Idiomas.FirstOrDefault(i => Igual(i.Idioma, idioma));
            if (existente is null)
                return laboral with { Idiomas = [.. laboral.Idiomas, new IdiomaUsuario(idioma, nivel)] };

            if (existente.Nivel == nivel) return null;

            return laboral with
            {
                Idiomas = [.. laboral.Idiomas.Select(i => Igual(i.Idioma, idioma) ? i with { Nivel = nivel } : i)]
            };
        }, ct);
    }

    private async Task<ResultadoHerramienta> ActualizarResumenAsync(JsonElement a, CancellationToken ct)
    {
        var resumen = Cadena(a, "resumen");
        if (resumen is null) return Error("Falta 'resumen'.");
        if (resumen.Length > MaxLargoResumen)
            return Error($"El resumen no puede superar los {MaxLargoResumen} caracteres.");

        return await ModificarLaboralAsync("Resumen guardado.", laboral =>
            laboral.Resumen == resumen ? null : laboral with { Resumen = resumen }, ct);
    }

    private async Task<ResultadoHerramienta> IndicarSinExperienciaAsync(CancellationToken ct)
    {
        return await ModificarLaboralAsync("Se registró que todavía no tiene experiencia laboral.", laboral =>
        {
            if (laboral.Experiencias.Count > 0)
                throw new ReglaPerfilException("Ya tiene experiencias cargadas, así que no se puede marcar que no tiene experiencia.");

            return laboral.SinExperiencia ? null : laboral with { SinExperiencia = true };
        }, ct);
    }

    /// <summary>
    /// Lee el perfil vigente, aplica <paramref name="cambio"/> y lo guarda completo.
    /// Si el cambio devuelve null es que el dato ya estaba cargado y no hay nada que guardar.
    /// </summary>
    private async Task<ResultadoHerramienta> ModificarLaboralAsync(
        string mensajeExito, Func<PerfilLaboralUsuario, PerfilLaboralUsuario?> cambio, CancellationToken ct)
    {
        var actual = await perfiles.ObtenerAsync(accessToken, ct);
        if (actual is null)
            return Error("No se pudo consultar el perfil actual, así que no se guardó nada.");

        var laboral = actual.Laboral ?? new PerfilLaboralUsuario(null, false, [], [], [], []);

        PerfilLaboralUsuario? nuevo;
        try
        {
            nuevo = cambio(laboral);
        }
        catch (ReglaPerfilException ex)
        {
            return Error($"No se guardó: {ex.Message}");
        }

        if (nuevo is null)
            return new ResultadoHerramienta("Ese dato ya estaba cargado en el perfil; no hizo falta cambiar nada.", EsError: false);

        var resultado = await perfiles.GuardarPerfilLaboralAsync(accessToken, nuevo, ct);

        return resultado.Exito
            ? new ResultadoHerramienta(mensajeExito, EsError: false)
            : Error($"No se guardó: {resultado.Mensaje}");
    }

    // ---------- datos personales y preferencias ----------

    private async Task<ResultadoHerramienta> ActualizarDatosPersonalesAsync(JsonElement a, CancellationToken ct)
    {
        var cambios = new ActualizacionDatosPersonales(
            Cadena(a, "nombre"), Cadena(a, "apellido"), Cadena(a, "telefono"));

        if (cambios.Nombre is null && cambios.Apellido is null && cambios.Telefono is null)
            return Error("Indicá al menos uno de: nombre, apellido, telefono.");

        var resultado = await perfiles.ActualizarDatosPersonalesAsync(accessToken, cambios, ct);

        return resultado.Exito
            ? new ResultadoHerramienta("Datos personales guardados.", EsError: false)
            : Error($"No se guardó: {resultado.Mensaje}");
    }

    private async Task<ResultadoHerramienta> ActualizarPreferenciasAsync(JsonElement a, CancellationToken ct)
    {
        var campos = new (string Argumento, string Clave, string[]? Validos)[]
        {
            ("puestos_interes", "puestosInteres", null),
            ("ubicaciones", "ubicaciones", null),
            ("modalidades", "modalidades", Modalidades),
            ("excluir", "excluir", null),
        };

        var nuevos = new List<(string Clave, JsonArray Valores)>();
        foreach (var (argumento, clave, validos) in campos)
        {
            if (!a.TryGetProperty(argumento, out var elemento) || elemento.ValueKind == JsonValueKind.Null) continue;

            if (!TryLista(elemento, argumento, validos, out var valores, out var error)) return Error(error);
            nuevos.Add((clave, valores));
        }

        if (nuevos.Count == 0)
            return Error("Indicá al menos una de: puestos_interes, ubicaciones, modalidades, excluir.");

        var actual = await perfiles.ObtenerAsync(accessToken, ct);
        if (actual is null)
            return Error("No se pudo consultar el perfil actual, así que no se guardó nada.");

        // Se conservan las claves que ya existían y que esta herramienta no maneja.
        JsonObject preferencias;
        try { preferencias = JsonNode.Parse(actual.PreferenciasJson) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { preferencias = new JsonObject(); }

        foreach (var (clave, valores) in nuevos)
            preferencias[clave] = valores;

        var resultado = await perfiles.ActualizarDatosPersonalesAsync(
            accessToken, new ActualizacionDatosPersonales(PreferenciasJson: preferencias.ToJsonString()), ct);

        return resultado.Exito
            ? new ResultadoHerramienta("Preferencias guardadas.", EsError: false)
            : Error($"No se guardó: {resultado.Mensaje}");
    }

    // ---------- utilidades ----------

    private sealed class ReglaPerfilException(string message) : Exception(message);

    private static ResultadoHerramienta Error(string texto) => new(texto, EsError: true);

    private static bool Igual(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Texto recortado, o null si falta o está vacío.</summary>
    private static string? Cadena(JsonElement a, string nombre) =>
        a.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static bool? Booleano(JsonElement a, string nombre) =>
        a.TryGetProperty(nombre, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;

    /// <summary>Devuelve el valor con la grafía oficial si coincide sin distinguir mayúsculas, o null.</summary>
    private static string? Canonico(string? valor, string[] validos) =>
        valor is null ? null : validos.FirstOrDefault(v => string.Equals(v, valor, StringComparison.OrdinalIgnoreCase));

    private static bool TryFecha(JsonElement a, string nombre, bool obligatoria, out string? fecha, out string error)
    {
        fecha = null;
        error = "";

        var texto = Cadena(a, nombre);
        if (texto is null)
        {
            if (!obligatoria) return true;
            error = $"Falta '{nombre}' (formato AAAA-MM-DD).";
            return false;
        }

        if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseada))
        {
            error = $"'{nombre}' debe tener el formato AAAA-MM-DD (por ejemplo 2021-03-01).";
            return false;
        }

        fecha = parseada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryLista(JsonElement elemento, string nombre, string[]? validos, out JsonArray valores, out string error)
    {
        valores = [];
        error = "";

        if (elemento.ValueKind != JsonValueKind.Array)
        {
            error = $"'{nombre}' debe ser una lista de textos.";
            return false;
        }

        foreach (var item in elemento.EnumerateArray())
        {
            var texto = item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(texto)) continue;

            if (texto.Length > MaxLargoItem)
            {
                error = $"Cada elemento de '{nombre}' puede tener como máximo {MaxLargoItem} caracteres.";
                return false;
            }

            if (validos is not null)
            {
                texto = Canonico(texto, validos);
                if (texto is null)
                {
                    error = $"'{nombre}' solo admite: {string.Join(", ", validos)}.";
                    return false;
                }
            }

            if (valores.Any(v => Igual(v!.GetValue<string>(), texto))) continue;
            valores.Add(texto);
        }

        if (valores.Count > MaxItemsLista)
        {
            error = $"'{nombre}' admite como máximo {MaxItemsLista} elementos.";
            return false;
        }

        return true;
    }

    // ---------- armado de esquemas ----------

    private static JsonObject Texto(string descripcion) =>
        new() { ["type"] = "string", ["description"] = descripcion };

    private static JsonObject Booleano(string descripcion) =>
        new() { ["type"] = "boolean", ["description"] = descripcion };

    private static JsonObject Opciones(string descripcion, string[] valores) =>
        new()
        {
            ["type"] = "string",
            ["description"] = descripcion,
            ["enum"] = new JsonArray(valores.Select(v => (JsonNode)v).ToArray()),
        };

    private static JsonObject Lista(string descripcion) =>
        new()
        {
            ["type"] = "array",
            ["description"] = descripcion,
            ["items"] = new JsonObject { ["type"] = "string" },
        };

    private static JsonObject ListaOpciones(string descripcion, string[] valores) =>
        new()
        {
            ["type"] = "array",
            ["description"] = descripcion,
            ["items"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(valores.Select(v => (JsonNode)v).ToArray()),
            },
        };

    private static DefinicionHerramienta Def(string nombre, string descripcion, JsonObject propiedades, params string[] obligatorios) =>
        new(nombre, descripcion, new JsonObject
        {
            ["type"] = "object",
            ["properties"] = propiedades,
            ["required"] = new JsonArray(obligatorios.Select(o => (JsonNode)o).ToArray()),
        }.ToJsonString());
}
