namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;

    // Native Windows "Save As" dialog (comdlg32), so the user chooses where every converted
    // file goes and sees the destination before anything is written. No WinForms dependency.
    internal static class SaveDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public Int32 lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public String lpstrFilter;
            public String lpstrCustomFilter;
            public Int32 nMaxCustFilter;
            public Int32 nFilterIndex;
            public IntPtr lpstrFile;
            public Int32 nMaxFile;
            public String lpstrFileTitle;
            public Int32 nMaxFileTitle;
            public String lpstrInitialDir;
            public String lpstrTitle;
            public Int32 Flags;
            public Int16 nFileOffset;
            public Int16 nFileExtension;
            public String lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public String lpTemplateName;
            public IntPtr pvReserved;
            public Int32 dwReserved;
            public Int32 FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern Boolean GetSaveFileNameW(ref OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const Int32 OFN_OVERWRITEPROMPT = 0x2;
        private const Int32 OFN_PATHMUSTEXIST = 0x800;
        private const Int32 OFN_NOCHANGEDIR = 0x8;
        private const Int32 MaxPath = 1024;

        // Returns the chosen path, or null if the user cancelled.
        public static String Ask(String suggestedPath, ConversionFormat format)
        {
            String result = null;
            var owner = GetForegroundWindow();

            // Common dialogs need an STA thread; the plugin's action threads are MTA.
            var thread = new Thread(() => result = Show(owner, suggestedPath, format));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        private static String Show(IntPtr owner, String suggestedPath, ConversionFormat format)
        {
            var buffer = Marshal.AllocHGlobal(MaxPath * 2);
            try
            {
                var name = Path.GetFileName(suggestedPath) + "\0";
                Marshal.Copy(name.ToCharArray(), 0, buffer, Math.Min(name.Length, MaxPath));

                var ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf<OpenFileName>(),
                    hwndOwner = owner,
                    lpstrFilter = $"{format.DisplayName} (*.{format.Extension})\0*.{format.Extension}\0",
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = MaxPath,
                    lpstrInitialDir = Path.GetDirectoryName(suggestedPath),
                    lpstrTitle = $"Save converted {format.DisplayName} as",
                    lpstrDefExt = format.Extension,
                    Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                };

                return GetSaveFileNameW(ref ofn) ? Marshal.PtrToStringUni(buffer) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
