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

        public static ConversionResult Ok(String outputPath) =>
            new() { Success = true, OutputPath = outputPath, Message = $"Converted to {Path.GetFileName(outputPath)}" };

        public static ConversionResult Fail(String message) =>
            new() { Success = false, Message = message };
    }

    // Converts documents by driving a headless LibreOffice process.
    //
    // LibreOffice is invoked as:
    //     soffice --headless --norestore --convert-to <filter> --outdir <dir> <input>
    //
    // A private, per-conversion user-installation profile is used so conversions still work
    // when the user already has LibreOffice open, and concurrent conversions do not clash.
    public static class LibreOfficeConverter
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        // Attempts to locate the `soffice` executable: an explicit setting first, then the
        // standard install locations, then anything on the PATH.
        public static Boolean TryLocateSoffice(Plugin plugin, out String path)
        {
            var settings = new ConverterSettings(plugin);
            return TryLocateSoffice(settings.SofficePath, out path);
        }

        public static Boolean TryLocateSoffice(String configuredPath, out String path)
        {
            path = null;

            if (!String.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            {
                path = configuredPath;
                return true;
            }

            foreach (var candidate in CandidateSofficePaths())
            {
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }

            // Fall back to resolving via PATH.
            var onPath = ResolveFromPath(IsWindows() ? "soffice.exe" : "soffice");
            if (onPath != null)
            {
                path = onPath;
                return true;
            }

            return false;
        }

        public static ConversionResult Convert(Plugin plugin, String inputPath, ConversionFormat format)
        {
            if (format == null)
            {
                return ConversionResult.Fail("No output format selected.");
            }

            if (String.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            {
                return ConversionResult.Fail($"Input file not found: {inputPath}");
            }

            var settings = new ConverterSettings(plugin);

            if (!TryLocateSoffice(settings.SofficePath, out var soffice))
            {
                return ConversionResult.Fail(
                    "LibreOffice not found. Install it from libreoffice.org or set 'SofficePath' in the plugin settings.");
            }

            var outputDir = String.IsNullOrWhiteSpace(settings.OutputFolder)
                ? Path.GetDirectoryName(inputPath)
                : settings.OutputFolder;

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                return ConversionResult.Fail($"Cannot create output folder '{outputDir}': {ex.Message}");
            }

            // Guard against a no-op conversion (same extension, same folder).
            var expectedOutput = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + "." + format.Extension);
            if (String.Equals(Path.GetFullPath(expectedOutput), Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
            {
                return ConversionResult.Fail($"File is already a {format.DisplayName} in the output folder.");
            }

            var profileDir = Path.Combine(Path.GetTempPath(), "LogiConverter_" + Guid.NewGuid().ToString("N"));
            var profileUri = new Uri(profileDir).AbsoluteUri;

            var args = new List<String>
            {
                "--headless",
                "--norestore",
                "--nologo",
                "--nofirststartwizard",
                $"-env:UserInstallation={profileUri}",
                "--convert-to", format.Filter,
                "--outdir", outputDir,
                inputPath,
            };

            try
            {
                var (exitCode, stdout, stderr) = RunProcess(soffice, args, DefaultTimeout);

                if (File.Exists(expectedOutput))
                {
                    PluginLog.Info($"Converted '{inputPath}' -> '{expectedOutput}'");
                    return ConversionResult.Ok(expectedOutput);
                }

                var detail = String.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                PluginLog.Error($"Conversion failed (exit {exitCode}): {detail}");
                return ConversionResult.Fail($"Conversion failed. {Truncate(detail, 160)}".Trim());
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Conversion process error");
                return ConversionResult.Fail($"Conversion error: {ex.Message}");
            }
            finally
            {
                TryDeleteDirectory(profileDir);
            }
        }

        private static (Int32 exitCode, String stdout, String stderr) RunProcess(String fileName, IEnumerable<String> arguments, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) { stdout.AppendLine(e.Data); } };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) { stderr.AppendLine(e.Data); } };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit((Int32)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                throw new TimeoutException($"LibreOffice did not finish within {timeout.TotalSeconds:0} seconds.");
            }

            // Ensure async buffers are flushed.
            process.WaitForExit();

            return (process.ExitCode, stdout.ToString(), stderr.ToString());
        }

        private static IEnumerable<String> CandidateSofficePaths()
        {
            if (IsWindows())
            {
                var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
                yield return Path.Combine(pf, "LibreOffice", "program", "soffice.exe");
                yield return Path.Combine(pf86, "LibreOffice", "program", "soffice.exe");
            }
            else if (IsMacOs())
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

        private static String ResolveFromPath(String executable)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (String.IsNullOrEmpty(pathVar))
            {
                return null;
            }

            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                var candidate = Path.Combine(dir.Trim(), executable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Boolean IsWindows() => OperatingSystem.IsWindows();

        private static Boolean IsMacOs() => OperatingSystem.IsMacOS();

        private static String Truncate(String value, Int32 max) =>
            String.IsNullOrEmpty(value) || value.Length <= max ? value?.Trim() : value.Substring(0, max).Trim() + "…";

        private static void TryDeleteDirectory(String dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Temp profile cleanup is best effort.
            }
        }
    }
}
