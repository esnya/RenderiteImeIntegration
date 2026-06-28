using FrooxEngine;
using HarmonyLib;
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

        WindowsImeOverlayManager.Detach(__instance);
    }
}
