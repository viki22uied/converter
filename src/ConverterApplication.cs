namespace Loupedeck.ConverterPlugin
{
    using System;

    // The File Converter is a universal plugin and is not bound to a specific host
    // application, so these return empty. The class is kept to match the standard plugin
    // template shape.
    public class ConverterApplication : ClientApplication
    {
        public ConverterApplication()
        {
        }

        protected override String GetProcessName() => String.Empty;

        protected override String GetBundleName() => String.Empty;
    }
}
