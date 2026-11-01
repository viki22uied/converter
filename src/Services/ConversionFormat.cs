namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Describes a single output format the plugin can convert a file to.
    //
    // `Filter` is the value passed to LibreOffice's `--convert-to` option. Using the bare
    // target extension (e.g. "pdf", "docx", "pptx", "png") lets LibreOffice pick the correct
    // export filter based on the source document type, which keeps the plugin simple and
    // works across Writer / Calc / Impress / Draw source documents.
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
        // as those are the primary targets requested for the Actions Ring experience.
        public static readonly IReadOnlyList<ConversionFormat> All = new List<ConversionFormat>
        {
            new ConversionFormat("pdf",  "PDF",          "pdf",  "pdf"),
            new ConversionFormat("docx", "Word (DOCX)",  "docx", "docx"),
            new ConversionFormat("pptx", "PowerPoint (PPTX)", "pptx", "pptx"),
            new ConversionFormat("xlsx", "Excel (XLSX)", "xlsx", "xlsx"),
            new ConversionFormat("odt",  "OpenDocument (ODT)", "odt", "odt"),
            new ConversionFormat("txt",  "Plain Text",   "txt",  "txt:Text"),
            new ConversionFormat("html", "Web Page (HTML)", "html", "html"),
            new ConversionFormat("png",  "Image (PNG)",  "png",  "png"),
            new ConversionFormat("jpg",  "Image (JPG)",  "jpg",  "jpg"),
        };

        public static ConversionFormat ById(String id) => All.FirstOrDefault(f => f.Id == id);
    }
}
