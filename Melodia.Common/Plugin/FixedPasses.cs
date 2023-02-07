namespace Melodia.Common.Plugin;

using dnlib.DotNet;

public static class FixedPasses {
    public static void OpenInternalClasses(AssemblyDef assembly) {
        foreach (var module in assembly.Modules) {
            foreach (var type in module.Types) {
                // Make class public
                type.Attributes &= ~TypeAttributes.VisibilityMask;
                type.Attributes |= TypeAttributes.Public;

                // Make class members public
                foreach (var methods in type.Methods) {
                    methods.Attributes &= ~MethodAttributes.MemberAccessMask;
                    methods.Attributes |= MethodAttributes.Public;
                }
                foreach (var field in type.Fields) {
                    field.Attributes &= ~FieldAttributes.FieldAccessMask;
                    field.Attributes |= FieldAttributes.Public;
                }
                // TODO: Handle properties
            }
        }
    }
}
