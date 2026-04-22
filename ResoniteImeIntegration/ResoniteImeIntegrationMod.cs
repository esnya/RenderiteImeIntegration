using System.Reflection;
using HarmonyLib;
using ResoniteModLoader;
#if USE_RESONITE_HOT_RELOAD_LIB
using ResoniteHotReloadLib;
#endif

namespace ResoniteImeIntegration;

public sealed class ResoniteImeIntegrationMod : ResoniteMod
{
    private const string ModNamespace = "com.nekometer.esnya";
    private static readonly Assembly Assembly = typeof(ResoniteImeIntegrationMod).Assembly;
    private static readonly string HarmonyId = $"{ModNamespace}.{Assembly.GetName().Name}";
    private static readonly Harmony Harmony = new(HarmonyId);

    public override string Name =>
        Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? string.Empty;

    public override string Author =>
        Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

    public override string Version =>
        (
            Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? string.Empty
        ).Split('+')[0];

    public override string Link =>
        Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(meta => meta.Key == "RepositoryUrl")
            ?.Value ?? string.Empty;

    public override void OnEngineInit()
    {
        Initialize(this);
    }

#if USE_RESONITE_HOT_RELOAD_LIB
    public static void BeforeHotReload()
    {
        Cleanup();
    }

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
        Windows.WindowsImeOverlayManager.Teardown();
        Windows.WindowsTsfService.Teardown();
        ImeRuntime.Controller.Reset();
    }
}
