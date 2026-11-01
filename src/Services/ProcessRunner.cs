namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    // Small helpers for running external converter executables.
    internal static class ProcessRunner
    {
        public static (Int32 exitCode, String stdout, String stderr) Run(String fileName, IEnumerable<String> arguments, TimeSpan timeout)
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

                throw new TimeoutException($"'{Path.GetFileName(fileName)}' did not finish within {timeout.TotalSeconds:0} seconds.");
            }

            process.WaitForExit();
            return (process.ExitCode, stdout.ToString(), stderr.ToString());
        }

        public static String ResolveFromPath(String executable)
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

        public static String Tail(String primary, String secondary)
        {
            var text = String.IsNullOrWhiteSpace(primary) ? secondary : primary;
            if (String.IsNullOrWhiteSpace(text))
            {
                return String.Empty;
            }

            text = text.Trim();
            const Int32 max = 160;
            return text.Length <= max ? text : "…" + text.Substring(text.Length - max);
        }

        public static void TryDeleteDirectory(String dir)
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
                // Temp cleanup is best effort.
            }
        }
    }
}
