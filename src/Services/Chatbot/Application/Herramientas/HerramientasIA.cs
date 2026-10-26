namespace Application.Herramientas;

/// <summary>Herramienta que se le ofrece al modelo. El esquema es un JSON Schema de tipo "object".</summary>
public record DefinicionHerramienta(string Nombre, string Descripcion, string EsquemaJson);

/// <summary>Pedido del modelo de ejecutar una herramienta. Los argumentos llegan como un objeto JSON.</summary>
public record LlamadaHerramienta(string Nombre, string ArgumentosJson);

/// <summary>Lo que ve el modelo después de ejecutar una herramienta.</summary>
public record ResultadoHerramienta(string Texto, bool EsError);

public interface IEjecutorHerramientas
{
    IReadOnlyList<DefinicionHerramienta> Definiciones { get; }

    /// <summary>
    /// Ejecuta la herramienta. Los errores de negocio (datos inválidos, rechazo de otro servicio) se
    /// devuelven como resultado con <see cref="ResultadoHerramienta.EsError"/> para que el modelo pueda
    /// corregirlos o explicárselos a la persona; no se lanzan como excepción.
    /// </summary>
    Task<ResultadoHerramienta> EjecutarAsync(LlamadaHerramienta llamada, CancellationToken cancellationToken);
}

public static class LimitesHerramientas
{
    /// <summary>Máximo de idas y vueltas con herramientas dentro de un solo mensaje, para evitar bucles.</summary>
    public const int MaxRondas = 5;
}
