namespace ResoniteImeIntegration.Core;

internal static class ImeIntegrationOptions
{
    public static bool UseTsfFirst { get; } = GetBool("TEXTINPUTIME_USE_TSF", defaultValue: true);

    public static bool ShowFallbackOverlay { get; } =
        GetBool("TEXTINPUTIME_SHOW_FALLBACK_OVERLAY", defaultValue: true);

    public static bool ForceOverlay { get; } =
        GetBool("TEXTINPUTIME_FORCE_OVERLAY", defaultValue: false);

    public static bool VerboseLogging { get; } =
        GetBool("TEXTINPUTIME_VERBOSE", defaultValue: false);

    private static bool GetBool(string name, bool defaultValue)
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return defaultValue;
        }
    }
}
