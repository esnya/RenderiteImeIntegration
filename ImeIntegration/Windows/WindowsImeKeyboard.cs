using FrooxEngine;
using FrooxEngine.Input;

namespace ImeIntegration.Windows;

internal sealed class WindowsImeKeyboard : ISystemKeyboard
{
    public void ShowKeyboard(
        string text,
        KeyboardType keyboardType,
        bool autocorrection = true,
        bool multiline = false,
        bool secure = false,
        string textPlaceholder = "",
        int characterLimit = -1
    )
    { }

    public void HideKeyboard() { }

    public void Dispose() { }
}
