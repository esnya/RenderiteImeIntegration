using FrooxEngine;
using HarmonyLib;
using ResoniteImeIntegration.Core;
using ResoniteImeIntegration.Windows;

namespace ResoniteImeIntegration.Patches;

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

        ImeRuntime.Controller.Exit();
        WindowsImeOverlayManager.Detach(__instance);
        if (ImeIntegrationOptions.UseTsfFirst)
        {
            WindowsTsfService.DissociateFocus();
        }
    }
}
