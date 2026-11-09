using Application.Cvs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Usuarios.Infrastructure.Documentos;

/// <summary>Dibuja el CV en un PDF A4 con QuestPDF.</summary>
public class GeneradorPdfCv : IGeneradorDocumentoCv
{
    static GeneradorPdfCv()
    {
        // QuestPDF es gratis (licencia Community) para proyectos académicos, personas y empresas de menos de 1 millón de
        // dólares anuales; en otro caso hay que revisar la licencia: https://www.questpdf.com/license/
        QuestPDF.Settings.License = LicenseType.Community;

        // El CV es texto libre de una persona: puede traer caracteres que la fuente no tiene (un emoji).
        // Por defecto QuestPDF abortaría todo el documento; acá prefiere dibujar lo demás.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public string ContentType => ArchivosCv.ContentTypePdf;
    public string Extension => ".pdf";

    public byte[] Generar(IReadOnlyList<BloqueCv> bloques)
    {
        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10.5f).FontColor(Colors.Grey.Darken4));

                pagina.Content().Column(columna =>
                {
                    foreach (var bloque in bloques)
                    {
                        switch (bloque.Tipo)
                        {
                            case TipoBloque.Titulo:
                                columna.Item().PaddingBottom(4)
                                    .Text(bloque.Texto).FontSize(20).Bold().FontColor(Colors.Black);
                                break;

                            case TipoBloque.Subtitulo:
                                columna.Item().PaddingTop(10).PaddingBottom(4)
                                    .BorderBottom(0.75f).BorderColor(Colors.Grey.Medium).PaddingBottom(2)
                                    .Text(bloque.Texto).FontSize(12.5f).Bold().FontColor(Colors.Blue.Darken3);
                                break;

                            case TipoBloque.Vineta:
                                // Un solo texto con la viñeta adelante (y no una fila de dos columnas): así una
                                // viñeta muy larga puede partirse entre páginas sin romper el armado del documento.
                                columna.Item().PaddingLeft(10).Text(texto =>
                                {
                                    texto.Span("•  ");
                                    texto.Span(bloque.Texto);
                                });
                                break;

                            case TipoBloque.EspacioEnBlanco:
                                columna.Item().Height(6);
                                break;

                            default:
                                columna.Item().Text(bloque.Texto);
                                break;
                        }
                    }
                });
            });
        }).GeneratePdf();
    }
}
