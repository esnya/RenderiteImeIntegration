using System.Reflection;
using Renderite.Shared;

namespace ImeIntegration.Core;

internal static class RenderiteCompositionContract
{
    private static readonly Type KeyboardStateType = typeof(KeyboardState);

    public static readonly FieldInfo? CompositionActiveField = KeyboardStateType.GetField("compositionActive");
    public static readonly FieldInfo? CompositionTextField = KeyboardStateType.GetField("compositionText");
    public static readonly FieldInfo? CompositionSelectionStartField = KeyboardStateType.GetField("compositionSelectionStart");
    public static readonly FieldInfo? CompositionSelectionLengthField = KeyboardStateType.GetField("compositionSelectionLength");
    public static readonly FieldInfo? CompositionCandidatesField = KeyboardStateType.GetField("compositionCandidates");
    public static readonly FieldInfo? CompositionCandidateIndexField = KeyboardStateType.GetField("compositionCandidateIndex");

    public static bool IsSupported =>
        CompositionActiveField is not null
        && CompositionTextField is not null
        && CompositionSelectionStartField is not null
        && CompositionSelectionLengthField is not null
        && CompositionCandidatesField is not null
        && CompositionCandidateIndexField is not null;

    public static string Describe() =>
        IsSupported
            ? "KeyboardState composition contract available."
            : "KeyboardState composition contract missing; renderer/shared message support not present.";
}
