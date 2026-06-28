using FrooxEngine;
using HarmonyLib;
using ImeIntegration.Windows;
using Renderite.Shared;

namespace ImeIntegration.Patches;

[HarmonyPatch(typeof(InputInterface))]
internal static class InputInterfacePatches
{
    private static readonly Key[] CompositionSuppressedKeys =
    [
        Key.LeftArrow,
        Key.RightArrow,
        Key.UpArrow,
        Key.DownArrow,
        Key.Home,
        Key.End,
        Key.PageUp,
        Key.PageDown,
        Key.Insert,
        Key.Backspace,
        Key.Delete,
        Key.Return,
        Key.KeypadEnter,
        Key.Escape,
        Key.Tab,
        Key.Space,
        Key.F6,
        Key.F7,
        Key.F8,
        Key.F9,
        Key.F10,
    ];

    [HarmonyPatch("HideKeyboard")]
    [HarmonyPrefix]
    private static void HideKeyboardPrefix()
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsImeOverlayManager.Teardown();
        }
    }

    [HarmonyPatch("UpdateKeyboardState")]
    [HarmonyPrefix]
    private static void UpdateKeyboardStatePrefix(KeyboardState keyboardState)
    {
        if (!OperatingSystem.IsWindows() || keyboardState?.compositionActive != true)
        {
            return;
        }

        var heldKeys = keyboardState.heldKeys;
        if (heldKeys is null)
        {
            return;
        }

        foreach (var key in CompositionSuppressedKeys)
        {
            heldKeys.Remove(key);
        }
    }

    [HarmonyPatch("UpdateKeyboardState")]
    [HarmonyPostfix]
    private static void UpdateKeyboardStatePostfix(InputInterface __instance, KeyboardState keyboardState)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsImeOverlayManager.UpdateComposition(
            __instance,
            keyboardState?.compositionActive == true,
            keyboardState?.compositionText
        );
    }

}
