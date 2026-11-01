namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;

    // Returns the file currently selected in the foreground Windows Explorer window, so
    // pressing a key converts the file the user clicked rather than the newest download.
    // Pressing a Creative Console key does not steal focus, so Explorer is still foreground.
    internal static class ExplorerSelection
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public static String TryGetSelectedFile()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            var foreground = GetForegroundWindow().ToInt64();
            String result = null;

            // Shell COM objects want an STA thread; the plugin's action threads are MTA.
            var thread = new Thread(() => result = Query(foreground));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(3));
            return result;
        }

        private static String Query(Int64 foreground)
        {
            try
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                // ponytail: Windows 11 Explorer tabs share one HWND; takes the first tab with a
                // file selected. Match the active tab via IShellBrowser if that proves wrong.
                foreach (dynamic window in shell.Windows())
                {
                    try
                    {
                        if ((Int64)window.HWND != foreground)
                        {
                            continue;
                        }

                        foreach (dynamic item in window.Document.SelectedItems())
                        {
                            if (!(Boolean)item.IsFolder)
                            {
                                return (String)item.Path;
                            }
                        }
                    }
                    catch
                    {
                        // Not a file-system Explorer window (e.g. Internet Explorer remnants); skip.
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Verbose($"Explorer selection unavailable: {ex.Message}");
            }

            return null;
        }
    }
}
