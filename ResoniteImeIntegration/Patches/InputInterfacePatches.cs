using FrooxEngine;
using FrooxEngine.Input;
using HarmonyLib;
using ResoniteImeIntegration.Windows;

namespace ResoniteImeIntegration.Patches;

[HarmonyPatch(typeof(InputInterface))]
internal static class InputInterfacePatches
{
    [HarmonyPatch("ShowKeyboard")]
    [HarmonyPrefix]
    private static void ShowKeyboardPrefix(InputInterface __instance)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (__instance.SystemKeyboard is null)
            {
                __instance.RegisterTouchKeyboard(new WindowsImeKeyboard());
            }
        }
        catch
        {
            // Keep stock keyboard behavior if our shim fails.
        }
    }

    [HarmonyPatch("ShowKeyboard")]
    [HarmonyPostfix]
    private static void ShowKeyboardPostfix()
    {
        if (OperatingSystem.IsWindows())
        {
            ImeRuntime.Controller.Enter();
        }
    }

    [HarmonyPatch("HideKeyboard")]
    [HarmonyPrefix]
    private static void HideKeyboardPrefix()
    {
        if (OperatingSystem.IsWindows())
        {
            ImeRuntime.Controller.Exit();
        }
    }
}
