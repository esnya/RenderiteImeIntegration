using ImeIntegration.Core;
using ResoniteModLoader;

namespace ImeIntegration;

internal static class ImeRuntime
{
    public static ImeCompositionState Composition { get; } = new();

    public static ImeStateController Controller { get; } =
        new(
            onEnable: () => DebugLog(() => "IME session entered."),
            onDisable: () =>
            {
                Composition.Clear();
                DebugLog(() => "IME session exited.");
            }
        );

    public static void DebugLog(Func<string> messageFactory)
    {
        try
        {
            ResoniteMod.DebugFunc(() => $"[ImeIntegration] {messageFactory()}");
        }
        catch
        {
            // Logging should never break input flow.
        }
    }
}
