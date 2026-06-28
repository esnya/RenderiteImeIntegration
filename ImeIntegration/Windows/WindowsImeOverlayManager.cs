using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using Renderite.Shared;

namespace ImeIntegration.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsImeOverlayManager
{
    private sealed class Overlay
    {
        public required Slot Slot { get; init; }
        public required TextRenderer Preedit { get; init; }
        public required InputInterface Input { get; init; }
        public required TextEditor Editor { get; init; }
    }

    private static readonly ConcurrentDictionary<TextEditor, Overlay> Overlays = new();
    private static readonly ConcurrentDictionary<TextEditor, InputInterface> ActiveEditors = new();

    public static void Attach(TextEditor editor)
    {
        if (editor.Slot.IsDestroyed)
        {
            return;
        }

        ActiveEditors[editor] = editor.InputInterface;

        try
        {
            var targetText = editor.Text?.Target as TextRenderer;
            var targetUixText = editor.Text?.Target as FrooxEngine.UIX.Text;
            var overlayParent =
                ResolveOverlayParent(targetText, targetUixText)
                ?? (editor.Text?.Target as Component)?.Slot
                ?? editor.Slot;
            var overlaySlot = overlayParent.AddLocalSlot("IME Composition Overlay", false);
            overlaySlot.PersistentSelf = false;
            overlaySlot.LocalPosition = float3.Zero;
            overlaySlot.LocalRotation = floatQ.Identity;
            overlaySlot.LocalScale = float3.One;
            overlaySlot.ActiveSelf = false;

            var preeditSlot = overlaySlot.AddSlot("Preedit");
            var preedit = preeditSlot.AttachComponent<TextRenderer>();
            var material = preeditSlot.AttachComponent<TextUnlitMaterial>();
            preedit.Material.Target = material;
            preedit.Size.Value = targetText?.Size.Value ?? 0.18f;
            preedit.Color.Value = colorX.White;
            preedit.Align = Alignment.TopLeft;
            preedit.Slot.LocalPosition = float3.Zero;
            material.FaceDilate.Value = 0.15f;
            material.OutlineThickness.Value = 0.15f;
            material.OutlineColor.Value = colorX.Black.SetA(0.85f);

            if (Overlays.TryRemove(editor, out var existing))
            {
                existing.Slot.Destroy();
            }

            Overlays[editor] = new Overlay
            {
                Slot = overlaySlot,
                Preedit = preedit,
                Input = editor.InputInterface,
                Editor = editor,
            };
        }
        catch
        {
            // Keep text input usable if overlay creation fails.
        }
    }

    public static void Detach(TextEditor editor)
    {
        ActiveEditors.TryRemove(editor, out _);

        if (!Overlays.TryRemove(editor, out var overlay))
        {
            return;
        }

        try
        {
            overlay.Slot.Destroy();
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    public static void Teardown()
    {
        ActiveEditors.Clear();

        foreach (var overlay in Overlays.Values)
        {
            try
            {
                overlay.Slot.Destroy();
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        Overlays.Clear();
    }

    public static void UpdateComposition(InputInterface input, bool active, string? text)
    {
        var compositionText = active ? text ?? string.Empty : string.Empty;
        var showOverlay = !string.IsNullOrEmpty(compositionText);

        foreach (var overlay in Overlays.Values)
        {
            if (overlay.Input != input)
            {
                continue;
            }

            try
            {
                overlay.Slot.RunSynchronously(
                    () =>
                    {
                        if (overlay.Slot.IsDestroyed)
                        {
                            return;
                        }

                        UpdateOverlayTransform(overlay);
                        overlay.Preedit.Text.Value = compositionText;
                        overlay.Slot.ActiveSelf = showOverlay;
                    },
                    immediatellyIfPossible: false
                );
            }
            catch
            {
                // Keep text input usable if overlay update fails.
            }
        }
    }

    public static bool TryGetCursorWindowPosition(InputInterface input, out RenderVector2 position)
    {
        position = default;

        foreach (var item in ActiveEditors)
        {
            var editor = item.Key;
            if (item.Value != input || editor.Slot.IsDestroyed || editor.Text?.Target is null)
            {
                continue;
            }

            if (!TryGetCaretAnchor(editor, out var anchor, out var lineHeight))
            {
                continue;
            }

            var world = editor.Slot.World;
            if (
                world is null
                || !TryProjectWorldToWindow(world, input, anchor, out var caretPosition, out var depth, out var tan)
            )
            {
                continue;
            }

            var resolution = input.WindowResolution;
            var screenLineHeight = WorldLengthToWindowPixels(depth, lineHeight, resolution.y, tan);
            position = new RenderVector2(
                Math.Clamp(caretPosition.x + screenLineHeight * 0.12f, 0f, resolution.x),
                Math.Clamp(caretPosition.y + screenLineHeight * 1.15f, 0f, resolution.y)
            );
            return true;
        }

        return false;
    }

    private static bool TryGetCaretAnchor(TextEditor editor, out float3 anchor, out float lineHeight)
    {
        var targetText = editor.Text?.Target as TextRenderer;
        if (targetText is not null)
        {
            var metrics = ComputeTextRendererAnchor(targetText);
            anchor = targetText.Slot.LocalPointToGlobal(metrics.Anchor);
            lineHeight = Math.Max(metrics.LineHeight, 0.05f);
            return true;
        }

        var targetUixText = editor.Text?.Target as FrooxEngine.UIX.Text;
        if (targetUixText is not null)
        {
            var canvas = targetUixText.RectTransform.Canvas;
            if (canvas is null)
            {
                anchor = default;
                lineHeight = 0f;
                return false;
            }

            var metrics = ComputeUixCaretAnchor(targetUixText);
            anchor = metrics.Anchor;
            lineHeight = Math.Max(metrics.WorldLineHeight, 0.05f);
            return true;
        }

        anchor = default;
        lineHeight = 0f;
        return false;
    }

    private static bool TryProjectWorldToWindow(
        World world,
        InputInterface input,
        float3 point,
        out RenderVector2 position,
        out float depth,
        out float tan
    )
    {
        position = default;
        depth = 0f;
        tan = 0f;

        world.GetViewTransform(out var viewPosition, out var viewRotation, out _);
        var localPoint = viewRotation.Inverted * (point - viewPosition);
        if (localPoint.z <= 0.0001f)
        {
            return false;
        }

        depth = localPoint.z;

        var resolution = input.WindowResolution;
        if (resolution.x <= 0 || resolution.y <= 0)
        {
            return false;
        }

        tan = MathF.Tan(world.GetFOV() * 0.5f * (MathF.PI / 180f));
        if (tan <= 0.0001f)
        {
            return false;
        }

        var aspect = world.GetAspect();
        var uv = new float2(
            0.5f + localPoint.x / (localPoint.z * tan * aspect * 2f),
            0.5f - localPoint.y / (localPoint.z * tan * 2f)
        );

        if (float.IsNaN(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.x) || float.IsInfinity(uv.y))
        {
            return false;
        }

        position = new RenderVector2(
            Math.Clamp(uv.x * resolution.x, 0f, resolution.x),
            Math.Clamp(uv.y * resolution.y, 0f, resolution.y)
        );
        return true;
    }

    private static float WorldLengthToWindowPixels(float depth, float worldLength, float windowHeight, float tan)
    {
        var visibleWorldHeight = depth * tan * 2f;
        if (visibleWorldHeight <= 0.0001f)
        {
            return 0f;
        }

        return Math.Max(worldLength, 0f) / visibleWorldHeight * windowHeight;
    }

    private static void UpdateOverlayTransform(Overlay overlay)
    {
        var targetText = overlay.Editor.Text?.Target as TextRenderer;
        if (targetText is not null)
        {
            var resolvedParent = ResolveOverlayParent(targetText, null);
            if (resolvedParent is not null && overlay.Slot.Parent != resolvedParent)
            {
                overlay.Slot.Parent = resolvedParent;
            }

            var metrics = ComputeTextRendererAnchor(targetText);
            overlay.Slot.GlobalPosition = targetText.Slot.LocalPointToGlobal(metrics.Anchor);
            overlay.Slot.GlobalRotation = targetText.Slot.GlobalRotation;
            overlay.Preedit.Size.Value = targetText.Size.Value;
            overlay.Preedit.Align = Alignment.TopLeft;
            overlay.Preedit.Slot.LocalPosition = float3.Zero;
            return;
        }

        var targetUixText = overlay.Editor.Text?.Target as FrooxEngine.UIX.Text;
        if (targetUixText is not null)
        {
            var resolvedParent = ResolveOverlayParent(null, targetUixText);
            if (resolvedParent is not null && overlay.Slot.Parent != resolvedParent)
            {
                overlay.Slot.Parent = resolvedParent;
            }

            if (!TryGetCaretAnchor(overlay.Editor, out var anchor, out var lineHeight))
            {
                overlay.Slot.ActiveSelf = false;
                return;
            }

            var canvasSlot = targetUixText.RectTransform.Canvas?.Slot;
            if (canvasSlot is null)
            {
                overlay.Slot.ActiveSelf = false;
                return;
            }

            var preeditSize = Math.Max(EstimateTextRendererSizeForLineHeight(overlay.Preedit, lineHeight), 0.05f);
            var offset =
                canvasSlot.Right * (preeditSize * 0.08f)
                + canvasSlot.Up * (preeditSize * 0.04f)
                - canvasSlot.Forward * Math.Max(preeditSize * 0.08f, 0.01f);
            overlay.Slot.GlobalPosition = anchor + offset;
            overlay.Slot.GlobalRotation = canvasSlot.GlobalRotation;
            overlay.Preedit.Size.Value = preeditSize;
            overlay.Preedit.Align = Alignment.TopLeft;
            overlay.Preedit.Slot.LocalPosition = float3.Zero;
        }
    }

    private static (float3 Anchor, float LineHeight) ComputeTextRendererAnchor(TextRenderer targetText)
    {
        var bounds = targetText.LocalBoundingBox;
        var lineHeight = Math.Max(MeasureText(targetText, "Mg").Height, 0.001f);
        var right = bounds.IsValid ? bounds.Center.x + bounds.Size.x * 0.5f : 0f;
        var bottom = bounds.IsValid ? bounds.Center.y - bounds.Size.y * 0.5f : 0f;
        var x = right + lineHeight * 0.15f;
        var y = bottom - lineHeight * 0.1f;
        return (new float3(x, y, 0f), lineHeight);
    }

    private static (float3 Anchor, float WorldLineHeight) ComputeUixCaretAnchor(FrooxEngine.UIX.Text targetText)
    {
        var text = targetText.Content.Value ?? string.Empty;
        var caretPosition = Math.Clamp(targetText.CaretPosition.Value, 0, text.Length);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        caretPosition = Math.Min(caretPosition, normalized.Length);

        var lineStart = normalized.LastIndexOf('\n', Math.Max(0, caretPosition - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var textBeforeLine = normalized[..lineStart];
        var textInLineBeforeCaret = normalized[lineStart..caretPosition];

        var canvas = targetText.RectTransform.Canvas;
        if (canvas is null)
        {
            return (float3.Zero, 0.03f);
        }

        var localRect = targetText.RectTransform.LocalComputeRect;
        var rect = targetText.RectTransform.ComputeGlobalComputeRect();
        var horizontalMetrics = targetText.RectTransform.GetHorizontalMetrics();
        var verticalMetrics = targetText.RectTransform.GetVerticalMetrics();
        var fullBase = MeasureText(targetText, normalized, autoSize: false);
        var prefixBase = MeasureText(targetText, textInLineBeforeCaret, autoSize: false);
        var beforeLineBase = MeasureText(targetText, textBeforeLine, autoSize: false);
        var rawLineHeight = Math.Max(MeasureText(targetText, "Mg", autoSize: false).Height, 0.001f);
        var effectiveScaleY = fullBase.Height > 0.0001f && verticalMetrics.preferred > 0.0001f
            ? Math.Clamp(verticalMetrics.preferred / fullBase.Height, 0.1f, 1f)
            : 1f;
        var effectiveLineHeight = rawLineHeight * effectiveScaleY;
        var heightBeforeLine = beforeLineBase.Height * effectiveScaleY;
        var renderedWidth = horizontalMetrics.preferred > 0.0001f
            ? Math.Min(horizontalMetrics.preferred, localRect.width)
            : Math.Min(fullBase.Width * effectiveScaleY, localRect.width);
        var effectiveScaleX = fullBase.Width > 0.0001f
            ? Math.Clamp(renderedWidth / fullBase.Width, 0f, 1f)
            : 1f;
        var widthBeforeCaret = Math.Min(prefixBase.Width * effectiveScaleX, renderedWidth);
        var lineOriginX = ComputeUixLineOriginX(rect, renderedWidth, targetText.HorizontalAlign);
        var canvasOrigin = canvas.Slot.LocalPointToGlobal(float3.Zero);
        var canvasUnitX = canvas.Slot.LocalPointToGlobal(float3.Right);
        var worldUnitsPerCanvasUnit = (canvasUnitX - canvasOrigin).Magnitude;
        var worldLineHeight = effectiveLineHeight * worldUnitsPerCanvasUnit;
        var localPoint = new float3(
            lineOriginX + widthBeforeCaret,
            rect.y + rect.height - heightBeforeLine,
            0f
        );
        return (canvas.Slot.LocalPointToGlobal(localPoint), worldLineHeight);
    }

    private static float ComputeUixLineOriginX(
        Rect rect,
        float renderedWidth,
        TextHorizontalAlignment horizontalAlign
    )
    {
        var leftX = rect.ExtentMin.x;
        var extraWidth = Math.Max(rect.width - renderedWidth, 0f);
        return horizontalAlign switch
        {
            TextHorizontalAlignment.Center => leftX + extraWidth * 0.5f,
            TextHorizontalAlignment.Right => leftX + extraWidth,
            _ => leftX,
        };
    }

    private static Slot? ResolveOverlayParent(TextRenderer? targetText, FrooxEngine.UIX.Text? targetUixText)
    {
        if (targetText is not null)
        {
            return targetText.Slot.World?.LocalUserSpace ?? targetText.Slot;
        }

        if (targetUixText is not null)
        {
            return targetUixText.Slot.World?.LocalUserSpace
                ?? targetUixText.RectTransform.Canvas?.Slot
                ?? targetUixText.Slot;
        }

        return null;
    }

    private static (float Width, float Height) MeasureText(TextRenderer template, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0f, 0f);
        }

        using var manager = new TextRenderManager();
        manager.PrepareCycle();
        manager.FontAssetSet = template.Font.Asset?.Sets ?? Array.Empty<FontAssetSet>();
        manager.String = text;
        manager.ParseRichText = (bool)template.ParseRichText || template.Text.Value == null;
        manager.Size = MathX.FilterInvalid((float)template.Size * 0.1f);
        manager.LineHeight = MathX.FilterInvalid(template.LineHeight);
        manager.Color = template.Color;
        manager.HorizontalAlign = template.HorizontalAlign;
        manager.VerticalAlign = template.VerticalAlign;
        manager.AlignmentMode = template.AlignmentMode;
        manager.VerticalAutoSize = template.VerticalAutoSize;
        manager.HorizontalAutoSize = template.HorizontalAutoSize;
        manager.MaskPattern = template.MaskPattern;
        manager.CaretPosition = -1;
        manager.SelectionStart = -1;
        manager.Bounded = template.Bounded;
        manager.BoundsSize = MathX.FilterInvalid(template.BoundsSize);
        manager.BoundsAlignment = template.BoundsAlignment;
        manager.MarkFontChanged();
        manager.Preprocess();
        manager.RunLayout();
        return (manager.ComputeLongestLine(), manager.ComputeTotalHeight());
    }

    private static (float Width, float Height) MeasureText(FrooxEngine.UIX.Text template, string text, bool autoSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0f, 0f);
        }

        using var manager = new TextRenderManager();
        manager.PrepareCycle();
        manager.FontAssetSet = template.Font.Asset?.Sets ?? Array.Empty<FontAssetSet>();
        manager.String = text;
        manager.ParseRichText = (bool)template.ParseRichText || template.Content.Value == null;
        manager.Size = MathX.FilterInvalid((float)template.Size);
        manager.LineHeight = MathX.FilterInvalid(template.LineHeight);
        manager.Color = template.Color;
        manager.HorizontalAlign = template.HorizontalAlign;
        manager.VerticalAlign = template.VerticalAlign;
        manager.AlignmentMode = template.AlignmentMode;
        manager.VerticalAutoSize = template.VerticalAutoSize;
        manager.HorizontalAutoSize = template.HorizontalAutoSize;
        manager.AutoSizeMin = template.AutoSizeMin;
        manager.AutoSizeMax = template.AutoSizeMax;
        manager.MaskPattern = template.MaskPattern;
        manager.CaretPosition = -1;
        manager.SelectionStart = -1;
        var rect = template.RectTransform.LocalComputeRect;
        manager.Bounded = true;
        manager.BoundsSize = new float2(rect.width, autoSize ? rect.height : 0f);
        manager.MarkFontChanged();
        manager.Preprocess();
        manager.RunLayout();
        return (manager.ComputeLongestLine(), manager.ComputeTotalHeight());
    }

    private static float EstimateTextRendererSizeForLineHeight(TextRenderer template, float desiredLineHeight)
    {
        if (desiredLineHeight <= 0f)
        {
            return (float)template.Size;
        }

        var measured = MeasureText(template, "Mg").Height;
        if (measured <= 0.0001f)
        {
            return (float)template.Size;
        }

        return (float)template.Size * (desiredLineHeight / measured);
    }
}
