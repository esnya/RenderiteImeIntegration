using ResoniteImeIntegration.Core;
using ResoniteModLoader;

namespace ResoniteImeIntegration;

internal static class ImeRuntime
{
    public static ImeStateController Controller { get; } =
        new(
            onEnable: () => SafeLog("IME session entered."),
            onDisable: () => SafeLog("IME session exited.")
        );

    private static void SafeLog(string message)
    {
        try
        {
            ResoniteMod.Msg($"[ResoniteImeIntegration] {message}");
        }
        catch
        {
            // Logging should never break input flow.
        }
    }
}
