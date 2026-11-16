using Recomendaciones.Application.DTOs;
using Recomendaciones.Application.Services;
using Xunit;

namespace Recomendaciones.Tests;

public class CompatibilidadServiceTests
{
    private readonly CompatibilidadService _service = new();

    private static AnalizarCompatibilidadRequest CrearRequest(
        List<string> habilidades,
        string requisitos)
    {
        return new AnalizarCompatibilidadRequest
        {
            UsuarioId = Guid.NewGuid(),
            EmpleoId = Guid.NewGuid(),
            Habilidades = habilidades,
            Requisitos = requisitos
        };
    }

    [Fact]
    public void TodasLasHabilidadesCoinciden_Retorna100()
    {
        var request = CrearRequest(
            new List<string> { "C#", "SQL", "React" },
            "C#,SQL,React"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(100m, resultado.Score);
        Assert.Equal(3, resultado.Coincidencias.Count);
        Assert.Empty(resultado.RequisitosFaltantes);
    }

    [Fact]
    public void MitadDeHabilidadesCoinciden_Retorna50()
    {
        var request = CrearRequest(
            new List<string> { "C#", "SQL" },
            "C#,SQL,Docker,Azure"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(50m, resultado.Score);
        Assert.Equal(2, resultado.Coincidencias.Count);
        Assert.Equal(2, resultado.RequisitosFaltantes.Count);
    }

    [Fact]
    public void NingunaHabilidadCoincide_Retorna0()
    {
        var request = CrearRequest(
            new List<string> { "React", "JavaScript" },
            "Python,Django,PostgreSQL"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(0m, resultado.Score);
        Assert.Empty(resultado.Coincidencias);
        Assert.Equal(3, resultado.RequisitosFaltantes.Count);
    }

    [Fact]
    public void ComparacionIgnoraMayusculasYMinusculas()
    {
        var request = CrearRequest(
            new List<string> { "react", "sql" },
            "REACT,SQL"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(100m, resultado.Score);
        Assert.Equal(2, resultado.Coincidencias.Count);
    }

    [Fact]
    public void HabilidadesDuplicadas_NoAlteranResultado()
    {
        var request = CrearRequest(
            new List<string> { "C#", "c#", "SQL", "sql" },
            "C#,SQL,Docker"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(66.67m, resultado.Score);
        Assert.Equal(2, resultado.Coincidencias.Count);
        Assert.Single(resultado.RequisitosFaltantes);
    }

    [Fact]
    public void RequisitosDuplicados_NoAlteranResultado()
    {
        var request = CrearRequest(
            new List<string> { "C#" },
            "C#,c#,SQL"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(50m, resultado.Score);
        Assert.Single(resultado.Coincidencias);
        Assert.Single(resultado.RequisitosFaltantes);
    }

    [Fact]
    public void RequisitosVacios_Retorna0()
    {
        var request = CrearRequest(
            new List<string> { "C#", "SQL" },
            ""
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(0m, resultado.Score);
        Assert.Empty(resultado.Coincidencias);
        Assert.Empty(resultado.RequisitosFaltantes);
    }

    [Fact]
    public void UsuarioSinHabilidades_Retorna0()
    {
        var request = CrearRequest(
            new List<string>(),
            "C#,SQL,React"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(0m, resultado.Score);
        Assert.Equal(3, resultado.RequisitosFaltantes.Count);
    }

    [Fact]
    public void EspaciosEnBlanco_NoAfectanComparacion()
    {
        var request = CrearRequest(
            new List<string> { " C# ", " SQL " },
            " C# , SQL , Docker "
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(66.67m, resultado.Score);
        Assert.Equal(2, resultado.Coincidencias.Count);
        Assert.Single(resultado.RequisitosFaltantes);
    }

    [Fact]
    public void ResultadoConservaIdentificadoresYMotivo()
    {
        var request = CrearRequest(
            new List<string> { "C#", "SQL" },
            "C#,SQL,Docker"
        );

        var resultado = _service.Analizar(request);

        Assert.Equal(request.UsuarioId, resultado.UsuarioId);
        Assert.Equal(request.EmpleoId, resultado.EmpleoId);
        Assert.Equal(
            "El usuario cumple 2 de 3 requisitos.",
            resultado.Motivo
        );
    }
}
