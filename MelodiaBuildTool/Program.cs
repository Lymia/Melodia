namespace Melodia.Patcher;

using System.IO;
using Melodia.Common.Plugin;

internal static class Program {
    internal static void Main(string[] args) {
        var loader = new PatcherContext(new string[] { "../contrib" });
        var assembly = loader.LoadAssembly("Crystal Project");
        FixedPasses.OpenInternalClasses(assembly);
        loader.MarkAssemblyModified("Crystal Project");

        var resolver = loader.ToResolver();
        File.WriteAllBytes("../contrib/Crystal Project.exe", resolver.GetOverrideData("Crystal Project"));
    }
}
