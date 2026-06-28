using System.Reflection;
using Renderite.Shared;

namespace ImeIntegration.Core;

internal static class RenderiteCompositionContract
{
    private static readonly Type KeyboardStateType = typeof(KeyboardState);
    private static readonly Type OutputStateType = typeof(OutputState);

    public static readonly FieldInfo? CompositionActiveField = KeyboardStateType.GetField("compositionActive");
    public static readonly FieldInfo? CompositionTextField = KeyboardStateType.GetField("compositionText");
    public static readonly FieldInfo? CompositionCursorPositionField =
        OutputStateType.GetField("compositionCursorPosition");

    public static bool IsSupported =>
        CompositionActiveField is not null
        && CompositionTextField is not null;

    public static bool IsCursorPositionSupported => CompositionCursorPositionField is not null;
}
