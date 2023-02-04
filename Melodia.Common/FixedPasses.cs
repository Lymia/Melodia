using dnlib.DotNet;

namespace Melodia.Common;

public static class FixedPasses {
    public static void OpenInternalClasses(AssemblyDef assembly) {
        foreach (var module in assembly.Modules) {
            foreach (var type in module.Types) {
                type.Attributes &= ~TypeAttributes.VisibilityMask;
                type.Attributes |= TypeAttributes.Public;
            }
        }
    }
}