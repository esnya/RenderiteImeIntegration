using FrooxEngine;
using HarmonyLib;
using ImeIntegration.Core;
using ImeIntegration.Windows;

namespace ImeIntegration.Patches;

[HarmonyPatch(typeof(TextEditor))]
internal static class TextEditorPatches
{
    [HarmonyPatch("Focus")]
    [HarmonyPostfix]
    private static void FocusPostfix(TextEditor __instance, User user)
    {
        if (!OperatingSystem.IsWindows() || user?.IsLocalUser != true)
        {
            return;
        }

        ImeRuntime.DebugLog(
            () =>
                $"TextEditor.Focus slot={__instance.Slot?.Name} textTarget={__instance.Text?.Target?.GetType().FullName ?? "null"} "
                + $"mask={(!string.IsNullOrEmpty(__instance.Text?.Target?.MaskPattern)).ToString()} depth={ImeRuntime.Controller.Depth}"
        );
        ImeRuntime.Controller.Enter();
        WindowsImeOverlayManager.Attach(__instance);
    }

    [HarmonyPatch("Defocus")]
    [HarmonyPrefix]
    private static void DefocusPrefix(TextEditor __instance, User user)
    {
        if (!OperatingSystem.IsWindows() || user?.IsLocalUser != true)
        {
            return;
        }

        ImeRuntime.DebugLog(
            () => $"TextEditor.Defocus slot={__instance.Slot?.Name} depth={ImeRuntime.Controller.Depth}"
        );
        ImeRuntime.Composition.Clear();
        ImeRuntime.Controller.Exit();
        WindowsImeOverlayManager.Detach(__instance);
    }
}
