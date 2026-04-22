using System.Runtime.Versioning;
using ResoniteImeIntegration.Core;

namespace ResoniteImeIntegration.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsIme
{
    public static string GetCompositionString()
    {
        if (ImeIntegrationOptions.UseTsfFirst && WindowsTsfService.IsAvailable())
        {
            var text = WindowsTsfService.GetCompositionString();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return WindowsImeService.GetCompositionString();
    }

    public static WindowsImeService.CandidateData? GetCandidateData()
    {
        if (ImeIntegrationOptions.UseTsfFirst && WindowsTsfService.IsAvailable())
        {
            var data = WindowsTsfService.GetCandidateData();
            if (data?.Items is { Length: > 0 })
            {
                return data;
            }
        }

        return WindowsImeService.GetCandidateData();
    }
}
