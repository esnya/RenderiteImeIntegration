using FrooxEngine;
using FrooxEngine.Input;
using HarmonyLib;
using ImeIntegration.Core;
using ImeIntegration.Windows;

namespace ImeIntegration.Patches;

[HarmonyPatch(typeof(InputInterface))]
internal static class InputInterfacePatches
{
    private static readonly WindowsImeKeyboard SharedKeyboard = new();

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
                __instance.RegisterTouchKeyboard(SharedKeyboard);
                ImeRuntime.DebugLog(() => "InputInterface.RegisterTouchKeyboard shared-keyboard");
            }
            else
            {
                ImeRuntime.DebugLog(
                    () =>
                        $"InputInterface.ShowKeyboard existing-system-keyboard={__instance.SystemKeyboard.GetType().FullName}"
                );
            }
        }
        catch
        {
            // Keep stock keyboard behavior if our shim fails.
        }
    }

    [HarmonyPatch("ShowKeyboard")]
    [HarmonyPostfix]
    private static void ShowKeyboardPostfix(
        InputInterface __instance,
        string currentText,
        object requestee,
        bool multiline,
        bool secure
    )
    {
        if (OperatingSystem.IsWindows())
        {
            ImeRuntime.DebugLog(
                () =>
                    $"InputInterface.ShowKeyboard focused={__instance.IsWindowFocused} vr={__instance.VR_Active} "
                    + $"keyboardActive={__instance.IsKeyboardActive} multiline={multiline} secure={secure} "
                    + $"requestee={requestee?.GetType().FullName ?? "null"} textLen={currentText?.Length ?? 0}"
            );
            ImeRuntime.Controller.Enter();
        }
    }

    [HarmonyPatch("HideKeyboard")]
    [HarmonyPrefix]
    private static void HideKeyboardPrefix()
    {
        if (OperatingSystem.IsWindows())
        {
            ImeRuntime.Composition.Clear();
            ImeRuntime.DebugLog(() => "InputInterface.HideKeyboard");
            ImeRuntime.Controller.Exit();
        }
    }

    [HarmonyPatch("UpdateKeyboardState")]
    [HarmonyPostfix]
    private static void UpdateKeyboardStatePostfix(object keyboardState)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (keyboardState is null)
        {
            ImeRuntime.Composition.Clear();
            return;
        }

        if (!RenderiteCompositionContract.IsSupported)
        {
            ImeRuntime.Composition.Clear();
            return;
        }

        var candidates =
            RenderiteCompositionContract.CompositionCandidatesField!.GetValue(keyboardState) as IReadOnlyList<string>;
        var active =
            RenderiteCompositionContract.CompositionActiveField!.GetValue(keyboardState) is bool boolValue
            && boolValue;
        var selectionStart =
            RenderiteCompositionContract.CompositionSelectionStartField!.GetValue(keyboardState) is int selectionStartValue
                ? selectionStartValue
                : 0;
        var selectionLength =
            RenderiteCompositionContract.CompositionSelectionLengthField!.GetValue(keyboardState) is int selectionLengthValue
                ? selectionLengthValue
                : 0;
        var candidateIndex =
            RenderiteCompositionContract.CompositionCandidateIndexField!.GetValue(keyboardState) is int candidateIndexValue
                ? candidateIndexValue
                : -1;
        ImeRuntime.Composition.Update(
            active,
            RenderiteCompositionContract.CompositionTextField!.GetValue(keyboardState) as string,
            selectionStart,
            selectionLength,
            candidates,
            candidateIndex
        );
    }
}
