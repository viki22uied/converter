namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    // The broad family a source document belongs to. Used only to word helpful messages;
    // the actual capability check is delegated to LibreOffice at conversion time.
    public enum DocumentFamily
    {
        Text,
        Spreadsheet,
        Presentation,
        Drawing,
        Pdf,
        Image,
        Unknown,
    }

    // Describes a single output format the plugin can convert a file to.
    //
    // `Filter` is the value passed to LibreOffice's `--convert-to` option. Using the bare
    // target extension (e.g. "pdf", "docx", "pptx", "png") lets LibreOffice pick the correct
    // export filter based on the source document type.
    public sealed class ConversionFormat
    {
        public String Id { get; }

        public String DisplayName { get; }

        public String Extension { get; }

        public String Filter { get; }

        private ConversionFormat(String id, String displayName, String extension, String filter)
        {
            this.Id = id;
            this.DisplayName = displayName;
            this.Extension = extension;
            this.Filter = filter;
        }

        // The ordered list of formats exposed as actions. PDF / Word / PowerPoint lead,
        // as those are the primary targets for the Actions Ring experience.
        public static readonly IReadOnlyList<ConversionFormat> All = new List<ConversionFormat>
        {
            new ConversionFormat("pdf",  "PDF",                "pdf",  "pdf"),
            new ConversionFormat("docx", "Word (DOCX)",        "docx", "docx"),
            new ConversionFormat("pptx", "PowerPoint (PPTX)",  "pptx", "pptx"),
            new ConversionFormat("xlsx", "Excel (XLSX)",       "xlsx", "xlsx"),
            new ConversionFormat("odt",  "OpenDocument (ODT)", "odt",  "odt"),
            new ConversionFormat("txt",  "Plain Text",         "txt",  "txt:Text"),
            new ConversionFormat("html", "Web Page (HTML)",    "html", "html"),
            new ConversionFormat("png",  "Image (PNG)",        "png",  "png"),
            new ConversionFormat("jpg",  "Image (JPG)",        "jpg",  "jpg"),
        };

        public static ConversionFormat ById(String id) =>
            All.FirstOrDefault(f => String.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

        public static ConversionFormat Pdf => ById("pdf");

        // Classifies a source file by extension, for friendly messaging.
        public static DocumentFamily FamilyOf(String path)
        {
            var ext = (Path.GetExtension(path) ?? String.Empty).ToLowerInvariant();
            switch (ext)
            {
                case ".doc": case ".docx": case ".docm": case ".dot": case ".dotx":
                case ".odt": case ".rtf": case ".txt": case ".md": case ".html": case ".htm":
                    return DocumentFamily.Text;
                case ".xls": case ".xlsx": case ".xlsm": case ".ods": case ".csv":
                    return DocumentFamily.Spreadsheet;
                case ".ppt": case ".pptx": case ".pps": case ".ppsx": case ".odp":
                    return DocumentFamily.Presentation;
                case ".odg": case ".svg": case ".wpd":
                    return DocumentFamily.Drawing;
                case ".pdf":
                    return DocumentFamily.Pdf;
                case ".png": case ".jpg": case ".jpeg": case ".gif": case ".bmp": case ".tif": case ".tiff":
                    return DocumentFamily.Image;
                default:
                    return DocumentFamily.Unknown;
            }
        }

        public static String FamilyNoun(DocumentFamily family) => family switch
        {
            DocumentFamily.Text => "document",
            DocumentFamily.Spreadsheet => "spreadsheet",
            DocumentFamily.Presentation => "presentation",
            DocumentFamily.Drawing => "drawing",
            DocumentFamily.Pdf => "PDF",
            DocumentFamily.Image => "image",
            _ => "file",
        };
    }
}
