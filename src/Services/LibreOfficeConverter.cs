namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    // The result of a conversion attempt.
    public sealed class ConversionResult
    {
        public Boolean Success { get; init; }

        public String OutputPath { get; init; }

        public String Message { get; init; }

        // True when the requested format could not be produced and a PDF was created instead.
        public Boolean UsedFallback { get; init; }

        public static ConversionResult Ok(String outputPath, String message, Boolean usedFallback = false) =>
            new() { Success = true, OutputPath = outputPath, Message = message, UsedFallback = usedFallback };

        public static ConversionResult Fail(String message) =>
            new() { Success = false, Message = message };
    }

    // One of the external conversion engines the plugin drives.
    public enum EngineKind
    {
        LibreOffice,
        Pandoc,
    }

    // Drives a headless LibreOffice process to perform a single conversion step.
    //
    //     soffice --headless --norestore --convert-to <filter> --outdir <dir> <input>
    //
    // A private user-installation profile (separate from the user's own) is used so conversions still work while
    // the user has LibreOffice open; steps share it one at a time.
    public static class LibreOfficeConverter
    {
        // Long enough for a 600-slide deck or a 200-page PDF on an ordinary PC. A step that fails
        // slowly stops the whole conversion (see SmartConverter), so this is also the longest wait.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        // One private profile, reused: building a fresh one costs about 4 seconds per step.
        // LibreOffice can't share a profile between running instances, so steps take turns.
        private static readonly String ProfileDir = Path.Combine(Path.GetTempPath(), "LogiConverter_profile");
        private static readonly Object ProfileLock = new();

        public static Boolean TryLocateSoffice(Plugin plugin, out String path) =>
            TryLocateSoffice(new ConverterSettings(plugin).SofficePath, out path);

        public static Boolean TryLocateSoffice(String configuredPath, out String path)
        {
            path = null;

            if (!String.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                path = configuredPath;
                return true;
            }

            foreach (var candidate in CandidatePaths())
            {
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }

            var onPath = ProcessRunner.ResolveFromPath(OperatingSystem.IsWindows() ? "soffice.exe" : "soffice");
            if (onPath != null)
            {
                path = onPath;
                return true;
            }

            return false;
        }

        // Runs a single LibreOffice conversion of `inputPath` to `target` into `outputDir`.
        // Returns the output file path on success, or null (with `error` set) on failure.
        public static String RunStep(Plugin plugin, String inputPath, ConversionFormat target, String outputDir, out String error)
        {
            error = null;

            if (!TryLocateSoffice(plugin, out var soffice))
            {
                error = "LibreOffice not found.";
                return null;
            }

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                error = $"Cannot create output folder: {ex.Message}";
                return null;
            }

            var args = new List<String>
            {
                "--headless",
                "--norestore",
                "--nologo",
                "--nofirststartwizard",
                $"-env:UserInstallation={new Uri(ProfileDir).AbsoluteUri}",
            };

            // Help LibreOffice open PDFs as editable documents (PDF -> Word, etc.).
            if (String.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(target.Extension, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("--infilter=writer_pdf_import");
            }

            args.Add("--convert-to");
            args.Add(target.Filter);
            args.Add("--outdir");
            args.Add(outputDir);
            args.Add(inputPath);

            try
            {
                Int32 exitCode;
                String stdout, stderr;
                lock (ProfileLock)
                {
                    (exitCode, stdout, stderr) = ProcessRunner.Run(soffice, args, DefaultTimeout);
                }

                var expected = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + "." + target.Extension);

                // An empty file is not a result (e.g. a spreadsheet saved as text).
                if (File.Exists(expected) && new FileInfo(expected).Length > 0)
                {
                    return expected;
                }

                error = $"LibreOffice could not produce {target.DisplayName} (exit {exitCode}). {ProcessRunner.Tail(stderr, stdout)}".Trim();
                return null;
            }
            catch (Exception ex) when (ex is not TimeoutException)
            {
                error = $"LibreOffice error: {ex.Message}";
                return null;
            }
        }

        private static IEnumerable<String> CandidatePaths()
        {
            if (OperatingSystem.IsWindows())
            {
                var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
                yield return Path.Combine(pf, "LibreOffice", "program", "soffice.exe");
                yield return Path.Combine(pf86, "LibreOffice", "program", "soffice.exe");
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/Applications/LibreOffice.app/Contents/MacOS/soffice";
            }
            else
            {
                yield return "/usr/bin/soffice";
                yield return "/usr/local/bin/soffice";
                yield return "/opt/libreoffice/program/soffice";
            }
        }
    }
}
