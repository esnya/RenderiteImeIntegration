using System.Reflection;
using HarmonyLib;
using ResoniteModLoader;
#if USE_RESONITE_HOT_RELOAD_LIB
using ResoniteHotReloadLib;
#endif

namespace ResoniteImeIntegration;

/// <summary>Entry point for the Windows desktop IME integration mod.</summary>
public sealed class ResoniteImeIntegrationMod : ResoniteMod
{
    private const string ModNamespace = "com.nekometer.esnya";
    private static readonly Assembly Assembly = typeof(ResoniteImeIntegrationMod).Assembly;
    private static readonly string HarmonyId = $"{ModNamespace}.{Assembly.GetName().Name}";
    private static readonly Harmony Harmony = new(HarmonyId);

    /// <inheritdoc />
    public override string Name =>
        Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? string.Empty;

    /// <inheritdoc />
    public override string Author =>
        Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

    /// <inheritdoc />
    public override string Version =>
        (
            Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? string.Empty
        ).Split('+')[0];

    /// <inheritdoc />
    public override string Link =>
        Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(meta => meta.Key == "RepositoryUrl")
            ?.Value ?? string.Empty;

    /// <inheritdoc />
    public override void OnEngineInit()
    {
        Initialize(this);
    }

#if USE_RESONITE_HOT_RELOAD_LIB
    /// <summary>Removes patches before Resonite hot reload swaps the assembly.</summary>
    public static void BeforeHotReload()
    {
        Cleanup();
    }

    /// <summary>Reapplies patches after Resonite hot reload finishes.</summary>
    public static void OnHotReload(ResoniteMod mod)
    {
        Initialize(mod);
    }
#endif

    private static void Initialize(ResoniteMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
#if USE_RESONITE_HOT_RELOAD_LIB
        HotReloader.RegisterForHotReload(mod);
#endif
        Harmony.PatchAll();
    }

    private static void Cleanup()
    {
        Harmony.UnpatchAll(HarmonyId);
        if (OperatingSystem.IsWindows())
        {
            Windows.WindowsImeOverlayManager.Teardown();
            Windows.WindowsTsfService.Teardown();
        }
        ImeRuntime.Controller.Reset();
    }
}
