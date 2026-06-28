using System.Reflection;
using Renderite.Shared;

namespace ImeIntegration.Core;

internal static class RenderiteCompositionContract
{
    private static readonly Type KeyboardStateType = typeof(KeyboardState);

    public static readonly FieldInfo? CompositionActiveField = KeyboardStateType.GetField("compositionActive");
    public static readonly FieldInfo? CompositionTextField = KeyboardStateType.GetField("compositionText");

    public static bool IsSupported =>
        CompositionActiveField is not null
        && CompositionTextField is not null;
}
