using System.Reflection;
using HarmonyLib;
using ImeIntegration.Core;
using ResoniteModLoader;

namespace ImeIntegration;

/// <summary>Entry point for the Windows desktop IME integration mod.</summary>
public sealed class ImeIntegrationMod : ResoniteMod
{
    private const string ModNamespace = "com.nekometer.esnya";
    private static readonly Assembly Assembly = typeof(ImeIntegrationMod).Assembly;
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
        if (!RenderiteCompositionContract.IsSupported)
        {
            return;
        }

        Harmony.PatchAll();
    }
}
