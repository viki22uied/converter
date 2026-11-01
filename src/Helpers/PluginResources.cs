namespace Loupedeck.ConverterPlugin
{
    using System;
    using System.IO;
    using System.Reflection;

    // A helper class for managing embedded plugin resources (e.g. icons).
    // Files handled here must have a Build Action of "Embedded Resource".
    internal static class PluginResources
    {
        private static Assembly _assembly;

        public static void Init(Assembly assembly)
        {
            assembly.CheckNullArgument(nameof(assembly));
            PluginResources._assembly = assembly;
        }

        public static String FindFile(String fileName) => PluginResources._assembly.FindFileOrThrow(fileName);

        public static Stream GetStream(String resourceName) => PluginResources._assembly.GetStream(PluginResources.FindFile(resourceName));

        public static Byte[] ReadBinaryFile(String resourceName) => PluginResources._assembly.ReadBinaryFile(PluginResources.FindFile(resourceName));

        public static BitmapImage ReadImage(String resourceName) => PluginResources._assembly.ReadImage(PluginResources.FindFile(resourceName));
    }
}
