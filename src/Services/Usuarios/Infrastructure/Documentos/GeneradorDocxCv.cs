using System.IO.Compression;
using System.Text;
using System.Xml;
using Application.Cvs;

namespace Usuarios.Infrastructure.Documentos;

/// <summary>
/// Escribe el CV como documento de Word (.docx) armando directamente sus partes XML dentro de un zip.
/// Un .docx válido mínimo necesita pocas piezas: el índice de tipos, la relación al documento,
/// los estilos base y el cuerpo.
/// </summary>
public class GeneradorDocxCv : IGeneradorDocumentoCv
{
    private const string NsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string NsRelaciones = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string NsTipos = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string TipoRelDocumento =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string TipoRelEstilos =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";

    public FormatoDocumentoCv Formato => FormatoDocumentoCv.Docx;
    public string ContentType => ArchivosCv.ContentTypeDocx;
    public string Extension => ".docx";

    public byte[] Generar(IReadOnlyList<BloqueCv> bloques)
    {
        using var salida = new MemoryStream();

        using (var zip = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true))
        {
            AgregarParte(zip, "[Content_Types].xml", EscribirTipos);
            AgregarParte(zip, "_rels/.rels", w => EscribirRelacion(w, "rId1", TipoRelDocumento, "word/document.xml"));
            AgregarParte(zip, "word/_rels/document.xml.rels", w => EscribirRelacion(w, "rId1", TipoRelEstilos, "styles.xml"));
            AgregarParte(zip, "word/styles.xml", EscribirEstilos);
            AgregarParte(zip, "word/document.xml", w => EscribirDocumento(w, bloques));
        }

        return salida.ToArray();
    }

    private static void AgregarParte(ZipArchive zip, string nombre, Action<XmlWriter> escribir)
    {
        var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
        using var flujo = entrada.Open();
        using var w = XmlWriter.Create(flujo, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            // Por defecto XmlWriter lanza una excepción si el texto trae un carácter que XML no admite.
            // Es lo que queremos: el texto ya llega limpio (TextoCv.Normalizar), y si alguna vez se
            // colara algo preferimos un error visible antes que entregar un .docx corrupto.
        });

        w.WriteStartDocument(standalone: true);
        escribir(w);
        w.WriteEndDocument();
    }

    private static void EscribirTipos(XmlWriter w)
    {
        w.WriteStartElement("Types", NsTipos);

        w.WriteStartElement("Default", NsTipos);
        w.WriteAttributeString("Extension", "rels");
        w.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
        w.WriteEndElement();

        w.WriteStartElement("Default", NsTipos);
        w.WriteAttributeString("Extension", "xml");
        w.WriteAttributeString("ContentType", "application/xml");
        w.WriteEndElement();

        w.WriteStartElement("Override", NsTipos);
        w.WriteAttributeString("PartName", "/word/document.xml");
        w.WriteAttributeString("ContentType",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
        w.WriteEndElement();

        w.WriteStartElement("Override", NsTipos);
        w.WriteAttributeString("PartName", "/word/styles.xml");
        w.WriteAttributeString("ContentType",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml");
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void EscribirRelacion(XmlWriter w, string id, string tipo, string destino)
    {
        w.WriteStartElement("Relationships", NsRelaciones);
        w.WriteStartElement("Relationship", NsRelaciones);
        w.WriteAttributeString("Id", id);
        w.WriteAttributeString("Type", tipo);
        w.WriteAttributeString("Target", destino);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    // Fuente y espaciado por defecto de todo el documento (las medidas de Word van en veinteavos de punto
    // y las de la fuente en medios puntos: 21 = 10,5 pt).
    private static void EscribirEstilos(XmlWriter w)
    {
        w.WriteStartElement("w", "styles", NsW);

        w.WriteStartElement("w", "docDefaults", NsW);

        w.WriteStartElement("w", "rPrDefault", NsW);
        w.WriteStartElement("w", "rPr", NsW);
        w.WriteStartElement("w", "rFonts", NsW);
        foreach (var atributo in new[] { "ascii", "hAnsi", "cs", "eastAsia" })
            w.WriteAttributeString("w", atributo, NsW, "Calibri");
        w.WriteEndElement();
        Valor(w, "sz", "21");
        Valor(w, "szCs", "21");
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("w", "pPrDefault", NsW);
        w.WriteStartElement("w", "pPr", NsW);
        w.WriteStartElement("w", "spacing", NsW);
        w.WriteAttributeString("w", "after", NsW, "40");
        w.WriteAttributeString("w", "line", NsW, "252");
        w.WriteAttributeString("w", "lineRule", NsW, "auto");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteEndElement(); // docDefaults

        w.WriteStartElement("w", "style", NsW);
        w.WriteAttributeString("w", "type", NsW, "paragraph");
        w.WriteAttributeString("w", "default", NsW, "1");
        w.WriteAttributeString("w", "styleId", NsW, "Normal");
        Valor(w, "name", "Normal");
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void EscribirDocumento(XmlWriter w, IReadOnlyList<BloqueCv> bloques)
    {
        w.WriteStartElement("w", "document", NsW);
        w.WriteStartElement("w", "body", NsW);

        foreach (var bloque in bloques)
        {
            switch (bloque.Tipo)
            {
                case TipoBloque.Titulo:
                    Parrafo(w, props: p => Espaciado(p, after: "80"),
                        runs: r => Texto(r, bloque.Texto, negrita: true, tamanio: "40"));
                    break;

                case TipoBloque.Subtitulo:
                    // Orden de los hijos de pPr fijado por el esquema: keepNext, pBdr, spacing, ind.
                    Parrafo(w, props: p =>
                        {
                            p.WriteStartElement("w", "keepNext", NsW);
                            p.WriteEndElement();
                            p.WriteStartElement("w", "pBdr", NsW);
                            p.WriteStartElement("w", "bottom", NsW);
                            p.WriteAttributeString("w", "val", NsW, "single");
                            p.WriteAttributeString("w", "sz", NsW, "6");
                            p.WriteAttributeString("w", "space", NsW, "1");
                            p.WriteAttributeString("w", "color", NsW, "808080");
                            p.WriteEndElement();
                            p.WriteEndElement();
                            Espaciado(p, before: "200", after: "80");
                        },
                        runs: r => Texto(r, bloque.Texto, negrita: true, tamanio: "26", color: "1F4E79"));
                    break;

                case TipoBloque.Vineta:
                    // Sangría francesa: la viñeta sale a la izquierda y el texto queda alineado después de ella.
                    Parrafo(w, props: p =>
                        {
                            p.WriteStartElement("w", "ind", NsW);
                            p.WriteAttributeString("w", "left", NsW, "360");
                            p.WriteAttributeString("w", "hanging", NsW, "260");
                            p.WriteEndElement();
                        },
                        runs: r =>
                        {
                            Texto(r, "•");
                            r.WriteStartElement("w", "r", NsW);
                            r.WriteStartElement("w", "tab", NsW);
                            r.WriteEndElement();
                            r.WriteEndElement();
                            Texto(r, bloque.Texto);
                        });
                    break;

                case TipoBloque.EspacioEnBlanco:
                    Parrafo(w, props: p =>
                    {
                        p.WriteStartElement("w", "spacing", NsW);
                        p.WriteAttributeString("w", "after", NsW, "0");
                        p.WriteAttributeString("w", "line", NsW, "120");
                        p.WriteAttributeString("w", "lineRule", NsW, "exact");
                        p.WriteEndElement();
                    }, runs: null);
                    break;

                default:
                    Parrafo(w, props: null, runs: r => Texto(r, bloque.Texto));
                    break;
            }
        }

        // Hoja A4 con márgenes de 2 cm (1134 veinteavos de punto).
        w.WriteStartElement("w", "sectPr", NsW);
        w.WriteStartElement("w", "pgSz", NsW);
        w.WriteAttributeString("w", "w", NsW, "11906");
        w.WriteAttributeString("w", "h", NsW, "16838");
        w.WriteEndElement();
        w.WriteStartElement("w", "pgMar", NsW);
        foreach (var lado in new[] { "top", "right", "bottom", "left" })
            w.WriteAttributeString("w", lado, NsW, "1134");
        w.WriteAttributeString("w", "header", NsW, "708");
        w.WriteAttributeString("w", "footer", NsW, "708");
        w.WriteAttributeString("w", "gutter", NsW, "0");
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteEndElement(); // body
        w.WriteEndElement(); // document
    }

    private static void Parrafo(XmlWriter w, Action<XmlWriter>? props, Action<XmlWriter>? runs)
    {
        w.WriteStartElement("w", "p", NsW);

        if (props is not null)
        {
            w.WriteStartElement("w", "pPr", NsW);
            props(w);
            w.WriteEndElement();
        }

        runs?.Invoke(w);
        w.WriteEndElement();
    }

    private static void Texto(
        XmlWriter w, string texto, bool negrita = false, string? tamanio = null, string? color = null)
    {
        w.WriteStartElement("w", "r", NsW);

        if (negrita || tamanio is not null || color is not null)
        {
            // Orden de los hijos de rPr fijado por el esquema: b, color, sz.
            w.WriteStartElement("w", "rPr", NsW);
            if (negrita)
            {
                w.WriteStartElement("w", "b", NsW);
                w.WriteEndElement();
            }
            if (color is not null) Valor(w, "color", color);
            if (tamanio is not null) Valor(w, "sz", tamanio);
            w.WriteEndElement();
        }

        w.WriteStartElement("w", "t", NsW);
        w.WriteAttributeString("xml", "space", null, "preserve");
        w.WriteString(texto);
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void Espaciado(XmlWriter w, string? before = null, string? after = null)
    {
        w.WriteStartElement("w", "spacing", NsW);
        if (before is not null) w.WriteAttributeString("w", "before", NsW, before);
        if (after is not null) w.WriteAttributeString("w", "after", NsW, after);
        w.WriteEndElement();
    }

    private static void Valor(XmlWriter w, string elemento, string valor)
    {
        w.WriteStartElement("w", elemento, NsW);
        w.WriteAttributeString("w", "val", NsW, valor);
        w.WriteEndElement();
    }
}
