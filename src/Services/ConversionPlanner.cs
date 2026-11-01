namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    // A single step in a conversion pipeline: run one engine to produce an intermediate
    // (or final) format.
    public sealed class ConversionStep
    {
        public EngineKind Engine { get; }

        public ConversionFormat Target { get; }

        public ConversionStep(EngineKind engine, ConversionFormat target)
        {
            this.Engine = engine;
            this.Target = target;
        }

        public override String ToString() => $"{this.Engine}->{this.Target.Id}";
    }

    // Works out ordered candidate pipelines to get from a source file to a requested format.
    //
    // The capability tables below are intentionally generous: each candidate is *attempted*
    // at runtime and only kept if it actually produces a file, so the planner's job is just
    // to propose sensible routes in a good order (fewest, highest-fidelity steps first).
    // DOCX is used as the universal "hub" intermediate because both engines read and write
    // it well; PDF is the final safety net handled by SmartConverter.
    public static class ConversionPlanner
    {
        // Formats Pandoc can read.
        private static readonly HashSet<String> PandocReads = new(StringComparer.OrdinalIgnoreCase)
        {
            "md", "markdown", "txt", "text", "html", "htm", "docx", "odt", "epub", "rst", "org", "tex", "latex",
        };

        // Output format ids Pandoc can write (of the ids this plugin exposes, plus the docx hub).
        private static readonly HashSet<String> PandocWrites = new(StringComparer.OrdinalIgnoreCase)
        {
            "docx", "pptx", "html", "txt", "odt",
        };

        // Extensions LibreOffice can open.
        private static readonly HashSet<String> LibreReads = new(StringComparer.OrdinalIgnoreCase)
        {
            "doc", "docx", "docm", "dot", "dotx", "odt", "rtf", "txt", "html", "htm",
            "xls", "xlsx", "xlsm", "ods", "csv",
            "ppt", "pptx", "pps", "ppsx", "odp",
            "pdf", "png", "jpg", "jpeg", "gif", "bmp", "tif", "tiff", "svg", "odg", "wpd",
        };

        // Output format ids LibreOffice can write (of the ids this plugin exposes, plus hubs).
        private static readonly HashSet<String> LibreWrites = new(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "docx", "odt", "txt", "html", "xlsx", "pptx", "png", "jpg",
        };

        public static IReadOnlyList<IReadOnlyList<ConversionStep>> BuildCandidates(String sourcePath, ConversionFormat target)
        {
            var src = (Path.GetExtension(sourcePath) ?? String.Empty).TrimStart('.').ToLowerInvariant();
            var docx = ConversionFormat.ById("docx");
            var pdf = ConversionFormat.Pdf;

            var candidates = new List<IReadOnlyList<ConversionStep>>();

            void Add(params ConversionStep[] steps) => candidates.Add(steps);

            // 1) Single-step Pandoc (best fidelity for Word<->PPT, Markdown/HTML -> *).
            if (PandocReads.Contains(src) && PandocWrites.Contains(target.Id))
            {
                Add(new ConversionStep(EngineKind.Pandoc, target));
            }

            // 2) Single-step LibreOffice.
            if (LibreReads.Contains(src) && LibreWrites.Contains(target.Id))
            {
                Add(new ConversionStep(EngineKind.LibreOffice, target));
            }

            // 3) Two-step via the DOCX hub, when the target isn't DOCX itself.
            if (!String.Equals(target.Id, "docx", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var first in FirstStepsToDocx(src, docx))
                {
                    if (PandocWrites.Contains(target.Id))
                    {
                        Add(first, new ConversionStep(EngineKind.Pandoc, target));
                    }

                    if (LibreWrites.Contains(target.Id))
                    {
                        Add(first, new ConversionStep(EngineKind.LibreOffice, target));
                    }
                }
            }

            // 4) Two-step via PDF (e.g. images from any printable document).
            if (!String.Equals(target.Id, "pdf", StringComparison.OrdinalIgnoreCase) && LibreReads.Contains(src))
            {
                if (LibreWrites.Contains(target.Id))
                {
                    Add(new ConversionStep(EngineKind.LibreOffice, pdf), new ConversionStep(EngineKind.LibreOffice, target));
                }
            }

            return Dedupe(candidates);
        }

        private static IEnumerable<ConversionStep> FirstStepsToDocx(String src, ConversionFormat docx)
        {
            // Prefer Pandoc to reach DOCX for markdown/html (keeps structure); otherwise LibreOffice.
            if (PandocReads.Contains(src))
            {
                yield return new ConversionStep(EngineKind.Pandoc, docx);
            }

            if (LibreReads.Contains(src))
            {
                yield return new ConversionStep(EngineKind.LibreOffice, docx);
            }
        }

        private static IReadOnlyList<IReadOnlyList<ConversionStep>> Dedupe(List<IReadOnlyList<ConversionStep>> candidates)
        {
            var seen = new HashSet<String>();
            var result = new List<IReadOnlyList<ConversionStep>>();
            foreach (var pipeline in candidates)
            {
                var key = String.Join("|", pipeline.Select(s => s.ToString()));
                if (seen.Add(key))
                {
                    result.Add(pipeline);
                }
            }

            return result;
        }
    }
}
