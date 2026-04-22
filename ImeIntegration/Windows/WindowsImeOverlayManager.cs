using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using ImeIntegration.Core;

namespace ImeIntegration.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsImeOverlayManager
{
    private sealed class Overlay
    {
        public required Slot Slot { get; init; }
        public required TextRenderer Preedit { get; init; }
        public required TextRenderer Candidates { get; init; }
        public required InputInterface Input { get; init; }
        public required TextEditor Editor { get; init; }
    }

    private static readonly ConcurrentDictionary<TextEditor, Overlay> Overlays = new();
    private static InputInterface? _subscribedInput;
    private static string? _lastLoggedState;
    private static string? _lastLoggedUixMetrics;
    private static string? _lastLoggedTextRendererMetrics;

    public static void Attach(TextEditor editor)
    {
        if (!ImeIntegrationOptions.ShowFallbackOverlay || editor.Slot.IsDestroyed)
        {
            return;
        }

        try
        {
            var targetText = editor.Text?.Target as TextRenderer;
            var targetUixText = editor.Text?.Target as FrooxEngine.UIX.Text;
            var overlayParent =
                ResolveOverlayParent(targetText, targetUixText)
                ?? (editor.Text?.Target as Component)?.Slot
                ?? editor.Slot;
            var overlaySlot = overlayParent.AddLocalSlot("IME Overlay", false);
            overlaySlot.PersistentSelf = false;
            overlaySlot.LocalPosition = float3.Zero;
            overlaySlot.LocalRotation = floatQ.Identity;
            overlaySlot.LocalScale = float3.One;

            var preeditSlot = overlaySlot.AddSlot("Preedit");
            var preedit = preeditSlot.AttachComponent<TextRenderer>();
            var preeditMaterial = preeditSlot.AttachComponent<TextUnlitMaterial>();
            preedit.Material.Target = preeditMaterial;
            preedit.Size.Value = targetText?.Size.Value ?? 0.18f;
            preedit.Color.Value = colorX.White;
            preedit.Align = targetText is not null ? Alignment.TopLeft : Alignment.BottomLeft;
            preeditMaterial.FaceDilate.Value = 0.15f;
            preeditMaterial.OutlineThickness.Value = 0.15f;
            preeditMaterial.OutlineColor.Value = colorX.Black.SetA(0.85f);

            var candidatesSlot = overlaySlot.AddSlot("Candidates");
            var candidates = candidatesSlot.AttachComponent<TextRenderer>();
            var candidatesMaterial = candidatesSlot.AttachComponent<TextUnlitMaterial>();
            candidates.Material.Target = candidatesMaterial;
            candidates.Size.Value = (targetText?.Size.Value ?? 0.18f) * 0.75f;
            candidates.Color.Value = new colorX(0.9f, 0.9f, 0.9f, 1f);
            candidates.Align = Alignment.TopLeft;
            candidatesMaterial.FaceDilate.Value = 0.15f;
            candidatesMaterial.OutlineThickness.Value = 0.15f;
            candidatesMaterial.OutlineColor.Value = colorX.Black.SetA(0.85f);

            if (Overlays.TryRemove(editor, out var existing))
            {
                existing.Slot.Destroy();
            }

            Overlays[editor] = new Overlay
            {
                Slot = overlaySlot,
                Preedit = preedit,
                Candidates = candidates,
                Input = editor.InputInterface,
                Editor = editor,
            };

            Subscribe(editor.InputInterface);
            UpdateOnce();
        }
        catch (Exception ex)
        {
            ImeRuntime.DebugLog(() => $"Overlay attach failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void Detach(TextEditor editor)
    {
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
        if (_subscribedInput is not null)
        {
            _subscribedInput.AfterInputsUpdate -= OnAfterInputsUpdate;
            _subscribedInput = null;
        }

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
        _lastLoggedState = null;
        _lastLoggedUixMetrics = null;
        _lastLoggedTextRendererMetrics = null;
    }

    private static void Subscribe(InputInterface input)
    {
        if (_subscribedInput == input)
        {
            return;
        }

        if (_subscribedInput is not null)
        {
            _subscribedInput.AfterInputsUpdate -= OnAfterInputsUpdate;
        }

        _subscribedInput = input;
        _subscribedInput.AfterInputsUpdate += OnAfterInputsUpdate;
    }

    private static void OnAfterInputsUpdate()
    {
        UpdateOnce();
    }

    private static void UpdateOnce()
    {
        var composition = ImeRuntime.Composition;
        var candidateText = FormatCandidates(composition.Candidates, composition.CandidateIndex);
        var inputTypeDelta = _subscribedInput?.TypeDelta ?? string.Empty;
        var showOverlay =
            ImeIntegrationOptions.ForceOverlay
            || (!string.IsNullOrEmpty(composition.Text) || !string.IsNullOrEmpty(candidateText));

        LogStateIfChanged(
            composition,
            inputTypeDelta,
            showOverlay
        );

        foreach (var overlay in Overlays.Values)
        {
            try
            {
                overlay.Slot.RunSynchronously(
                    () =>
                    {
                        if (overlay.Slot.IsDestroyed)
                        {
                            return;
                        }

                        UpdateTransform(overlay);
                        overlay.Preedit.Text.Value = composition.Text;
                        overlay.Candidates.Text.Value = candidateText;
                        overlay.Slot.ActiveSelf = showOverlay;
                    },
                    immediatellyIfPossible: false
                );
            }
            catch (Exception ex)
            {
                ImeRuntime.DebugLog(() => $"Overlay update failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void LogStateIfChanged(
        ImeCompositionState composition,
        string inputTypeDelta,
        bool showOverlay
    )
    {
        var candidateCount = composition.Candidates.Count;
        var state =
            $"compositionActive={composition.Active} compositionLen={composition.Text.Length} "
            + $"compositionSelectionStart={composition.SelectionStart} compositionSelectionLength={composition.SelectionLength} "
            + $"candidateCount={candidateCount} candidateIndex={composition.CandidateIndex} "
            + $"inputTypeDeltaLen={inputTypeDelta.Length} showOverlay={showOverlay} overlays={Overlays.Count}";
        if (state == _lastLoggedState)
        {
            return;
        }

        _lastLoggedState = state;
        ImeRuntime.DebugLog(() => state);
    }

    private static void UpdateTransform(Overlay overlay)
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
            overlay.Candidates.Size.Value = targetText.Size.Value * 0.75f;
            overlay.Preedit.Align = Alignment.TopLeft;
            overlay.Candidates.Align = Alignment.TopLeft;
            overlay.Preedit.Slot.LocalPosition = float3.Zero;
            overlay.Candidates.Slot.LocalPosition = float3.Down * metrics.LineHeight * 0.7f;
            LogTextRendererMetricsIfChanged(overlay, metrics);
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

            var metrics = ComputeUixCaretAnchor(targetUixText);
            var preeditSize = Math.Max(EstimateTextRendererSizeForLineHeight(overlay.Preedit, metrics.WorldLineHeight), 0.05f);
            var candidatesSize = Math.Max(preeditSize * 0.75f, 0.0375f);
            var canvasSlot = targetUixText.RectTransform.Canvas.Slot;
            var offset =
                canvasSlot.Right * (preeditSize * 0.08f)
                + canvasSlot.Up * (preeditSize * 0.04f)
                - canvasSlot.Forward * Math.Max(preeditSize * 0.08f, 0.01f);
            overlay.Slot.GlobalPosition = metrics.Anchor + offset;
            overlay.Slot.GlobalRotation = targetUixText.RectTransform.Canvas.Slot.GlobalRotation;
            overlay.Preedit.Size.Value = preeditSize;
            overlay.Candidates.Size.Value = candidatesSize;
            overlay.Preedit.Align = Alignment.TopLeft;
            overlay.Candidates.Align = Alignment.TopLeft;
            overlay.Preedit.Slot.LocalPosition = float3.Zero;
            overlay.Candidates.Slot.LocalPosition = float3.Down * metrics.WorldLineHeight * 0.7f;
            LogUixMetricsIfChanged(targetUixText, metrics);
            return;
        }

        overlay.Slot.LocalPosition = float3.Zero;
        overlay.Preedit.Align = Alignment.BottomLeft;
        overlay.Candidates.Align = Alignment.TopLeft;
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

    private static (
        float3 Anchor,
        float RawLineHeight,
        float EffectiveLineHeight,
        float WorldLineHeight,
        float UixSize,
        float UnitScale,
        float BaseFullWidth,
        float BasePrefixWidth,
        float RenderedFullWidth,
        float EffectiveScaleX,
        float LineOriginX,
        float WidthBeforeCaret,
        float HeightBeforeLine,
        float EffectiveScaleY,
        Rect LocalRect,
        Rect Rect
    ) ComputeUixCaretAnchor(FrooxEngine.UIX.Text targetText)
    {
        var text = targetText.Content.Value ?? string.Empty;
        var caretPosition = Math.Clamp(targetText.CaretPosition.Value, 0, text.Length);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        caretPosition = Math.Min(caretPosition, normalized.Length);

        var lineStart = normalized.LastIndexOf('\n', Math.Max(0, caretPosition - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var textBeforeLine = normalized[..lineStart];
        var textInLineBeforeCaret = normalized[lineStart..caretPosition];
        if (lineStart != 0 || normalized.Contains('\n', StringComparison.Ordinal))
        {
            return ComputeUixCaretAnchorFallback(targetText, normalized, textBeforeLine, textInLineBeforeCaret);
        }

        var canvas = targetText.RectTransform.Canvas;
        if (canvas is null)
        {
            return (float3.Zero, 0.03f, 0.03f, 0.03f, (float)targetText.Size, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, default(Rect), default(Rect));
        }

        var localRect = targetText.RectTransform.LocalComputeRect;
        var rect = targetText.RectTransform.ComputeGlobalComputeRect();
        var unitScale = Math.Max(canvas.UnitScale.Value, 0.0001f);
        var horizontalMetrics = targetText.RectTransform.GetHorizontalMetrics();
        var fullBase = MeasureText(targetText, normalized, autoSize: false);
        var fullAuto = MeasureText(targetText, normalized, autoSize: true);
        var prefixBase = MeasureText(targetText, textInLineBeforeCaret, autoSize: false);
        var rawLineHeight = Math.Max(MeasureText(targetText, "Mg", autoSize: false).Height, 0.001f);
        var effectiveScaleY = fullBase.Height > 0.0001f && fullAuto.Height > 0.0001f
            ? Math.Clamp(fullAuto.Height / fullBase.Height, 0.1f, 1f)
            : 1f;
        var effectiveLineHeight = rawLineHeight * effectiveScaleY;
        var heightBeforeLine = 0f;
        var baseFullWidth = fullBase.Width;
        var basePrefixWidth = prefixBase.Width;
        var renderedFullWidth = horizontalMetrics.preferred > 0.0001f
            ? Math.Min(horizontalMetrics.preferred, localRect.width)
            : Math.Min(baseFullWidth, localRect.width);
        var effectiveScaleX = baseFullWidth > 0.0001f
            ? Math.Clamp(renderedFullWidth / baseFullWidth, 0f, 1f)
            : 1f;
        var widthBeforeCaret = Math.Min(basePrefixWidth * effectiveScaleX, renderedFullWidth);
        var lineOriginX = ComputeUixLineOriginX(rect, renderedFullWidth, targetText.HorizontalAlign);
        var canvasOrigin = canvas.Slot.LocalPointToGlobal(float3.Zero);
        var canvasUnitX = canvas.Slot.LocalPointToGlobal(float3.Right);
        var worldUnitsPerCanvasUnit = (canvasUnitX - canvasOrigin).Magnitude;
        var worldLineHeight = effectiveLineHeight * worldUnitsPerCanvasUnit;

        var localPoint = new float3(
            lineOriginX + widthBeforeCaret,
            rect.y + rect.height - heightBeforeLine,
            0f
        );
        var globalPoint = canvas.Slot.LocalPointToGlobal(localPoint);
        return (
            globalPoint,
            rawLineHeight,
            effectiveLineHeight,
            worldLineHeight,
            (float)targetText.Size,
            unitScale,
            baseFullWidth,
            basePrefixWidth,
            renderedFullWidth,
            effectiveScaleX,
            lineOriginX,
            widthBeforeCaret,
            heightBeforeLine,
            effectiveScaleY,
            localRect,
            rect
        );
    }

    private static (
        float3 Anchor,
        float RawLineHeight,
        float EffectiveLineHeight,
        float WorldLineHeight,
        float UixSize,
        float UnitScale,
        float BaseFullWidth,
        float BasePrefixWidth,
        float RenderedFullWidth,
        float EffectiveScaleX,
        float LineOriginX,
        float WidthBeforeCaret,
        float HeightBeforeLine,
        float EffectiveScaleY,
        Rect LocalRect,
        Rect Rect
    ) ComputeUixCaretAnchorFallback(
        FrooxEngine.UIX.Text targetText,
        string normalized,
        string textBeforeLine,
        string textInLineBeforeCaret
    )
    {
        var canvas = targetText.RectTransform.Canvas;
        if (canvas is null)
        {
            return (float3.Zero, 0.03f, 0.03f, 0.03f, (float)targetText.Size, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, default(Rect), default(Rect));
        }

        var localRect = targetText.RectTransform.LocalComputeRect;
        var rect = targetText.RectTransform.ComputeGlobalComputeRect();
        var unitScale = Math.Max(canvas.UnitScale.Value, 0.0001f);
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
        var globalPoint = canvas.Slot.LocalPointToGlobal(localPoint);
        return (
            globalPoint,
            rawLineHeight,
            effectiveLineHeight,
            worldLineHeight,
            (float)targetText.Size,
            unitScale,
            fullBase.Width,
            prefixBase.Width,
            renderedWidth,
            effectiveScaleX,
            lineOriginX,
            widthBeforeCaret,
            heightBeforeLine,
            effectiveScaleY,
            localRect,
            rect
        );
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

    private static (float Width, float Height, float LineHeight) MeasureText(FrooxEngine.UIX.Text template, string text, bool autoSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0f, 0f, 0f);
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
        return (
            manager.ComputeLongestLine(),
            manager.ComputeTotalHeight(),
            Math.Max(manager.ComputeTotalHeight(), 0.001f)
        );
    }

    private static string FormatCandidates(IReadOnlyList<string> items, int selection)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        var rendered = new string[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            rendered[index] = index == selection ? $"[{items[index]}]" : items[index];
        }

        return string.Join("  ", rendered);
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

    private static void LogUixMetricsIfChanged(
        FrooxEngine.UIX.Text targetText,
        (
            float3 Anchor,
            float RawLineHeight,
            float EffectiveLineHeight,
            float WorldLineHeight,
            float UixSize,
            float UnitScale,
            float BaseFullWidth,
            float BasePrefixWidth,
            float RenderedFullWidth,
            float EffectiveScaleX,
            float LineOriginX,
            float WidthBeforeCaret,
            float HeightBeforeLine,
            float EffectiveScaleY,
            Rect LocalRect,
            Rect Rect
        ) metrics
    )
    {
        var rectTransform = targetText.RectTransform;
        var globalRect = rectTransform.ComputeGlobalComputeRect();
        var boundingRect = rectTransform.BoundingRect;
        var horizontalMetrics = rectTransform.GetHorizontalMetrics();
        var verticalMetrics = rectTransform.GetVerticalMetrics();
        var state =
            $"uixOverlay anchor=({metrics.Anchor.x:F4},{metrics.Anchor.y:F4},{metrics.Anchor.z:F4}) "
            + $"targetSize={(float)targetText.Size:F2} hAuto={(bool)targetText.HorizontalAutoSize} vAuto={(bool)targetText.VerticalAutoSize} "
            + $"autoMin={(float)targetText.AutoSizeMin:F2} autoMax={(float)targetText.AutoSizeMax:F2} "
            + $"rawLineHeight={metrics.RawLineHeight:F5} effectiveLineHeight={metrics.EffectiveLineHeight:F5} worldLineHeight={metrics.WorldLineHeight:F5} unitScale={metrics.UnitScale:F4} "
            + $"uixSize={metrics.UixSize:F2} "
            + $"baseFullWidth={metrics.BaseFullWidth:F4} basePrefixWidth={metrics.BasePrefixWidth:F4} renderedFullWidth={metrics.RenderedFullWidth:F4} "
            + $"effectiveScaleX={metrics.EffectiveScaleX:F4} effectiveScaleY={metrics.EffectiveScaleY:F4} lineOriginX={metrics.LineOriginX:F4} "
            + $"widthBeforeCaret={metrics.WidthBeforeCaret:F4} heightBeforeLine={metrics.HeightBeforeLine:F4} "
            + $"localRect=({metrics.LocalRect.x:F1},{metrics.LocalRect.y:F1},{metrics.LocalRect.width:F1},{metrics.LocalRect.height:F1}) "
            + $"rectMin=({metrics.Rect.ExtentMin.x:F1},{metrics.Rect.ExtentMin.y:F1}) "
            + $"rectMax=({metrics.Rect.ExtentMax.x:F1},{metrics.Rect.ExtentMax.y:F1}) "
            + $"globalRect=({globalRect.x:F1},{globalRect.y:F1},{globalRect.width:F1},{globalRect.height:F1}) "
            + $"boundingRect=({boundingRect.x:F1},{boundingRect.y:F1},{boundingRect.width:F1},{boundingRect.height:F1}) "
            + $"hMetrics(min={horizontalMetrics.min:F2},pref={horizontalMetrics.preferred:F2},flex={horizontalMetrics.flexible:F2}) "
            + $"vMetrics(min={verticalMetrics.min:F2},pref={verticalMetrics.preferred:F2},flex={verticalMetrics.flexible:F2})";
        if (state == _lastLoggedUixMetrics)
        {
            return;
        }

        _lastLoggedUixMetrics = state;
        ImeRuntime.DebugLog(() => state);
    }

    private static void LogTextRendererMetricsIfChanged(
        Overlay overlay,
        (float3 Anchor, float LineHeight) metrics
    )
    {
        var state =
            $"textRendererOverlay globalPos=({overlay.Slot.GlobalPosition.x:F4},{overlay.Slot.GlobalPosition.y:F4},{overlay.Slot.GlobalPosition.z:F4}) "
            + $"globalRot=({overlay.Slot.GlobalRotation.x:F4},{overlay.Slot.GlobalRotation.y:F4},{overlay.Slot.GlobalRotation.z:F4},{overlay.Slot.GlobalRotation.w:F4}) "
            + $"preeditSize={(float)overlay.Preedit.Size:F5} candidatesSize={(float)overlay.Candidates.Size:F5} "
            + $"candidateOffset={(metrics.LineHeight * 0.7f):F5}";
        if (state == _lastLoggedTextRendererMetrics)
        {
            return;
        }

        _lastLoggedTextRendererMetrics = state;
        ImeRuntime.DebugLog(() => state);
    }
}
