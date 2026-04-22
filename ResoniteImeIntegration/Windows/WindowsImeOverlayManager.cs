using System.Collections.Concurrent;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ResoniteImeIntegration.Core;

namespace ResoniteImeIntegration.Windows;

internal static class WindowsImeOverlayManager
{
    private sealed class Overlay
    {
        public required Slot Slot { get; init; }
        public required IText Preedit { get; init; }
        public required IText Candidates { get; init; }
        public required InputInterface Input { get; init; }
        public required Slot FollowSlot { get; init; }
    }

    private static readonly ConcurrentDictionary<TextEditor, Overlay> Overlays = new();
    private static InputInterface? _subscribedInput;

    public static void Attach(TextEditor editor)
    {
        if (!ImeIntegrationOptions.ShowFallbackOverlay || editor.Slot.IsDestroyed)
        {
            return;
        }

        try
        {
            var localUser = editor.LocalUser;
            var world = editor.World ?? Userspace.UserspaceWorld;
            var rootSlot = editor.LocalUserRoot?.Slot ?? world.RootSlot;

            localUser.GetPointInFrontOfUser(
                out float3 point,
                out floatQ rotation,
                float3.Backward,
                float3.Down * 0.15f,
                0.7f,
                true
            );

            var overlaySlot = rootSlot.AddLocalSlot("IME Overlay", false);
            overlaySlot.PersistentSelf = false;
            overlaySlot.GlobalPosition = point;
            overlaySlot.GlobalRotation = rotation;
            overlaySlot.LocalScale = float3.One * (editor.LocalUserRoot?.GlobalScale ?? 1f) * 0.001f;

            var canvas = overlaySlot.AttachComponent<Canvas>();
            canvas.Size.Value = new float2(ImeOverlayConfig.CanvasWidth, ImeOverlayConfig.CanvasHeight);
            overlaySlot.AttachComponent<RectTransform>();

            var ui = new UIBuilder(overlaySlot);
            var panelColor = new colorX(0f, 0f, 0f, ImeOverlayConfig.PanelAlpha);
            var panel = ui.Panel(panelColor);

            var preeditUi = new UIBuilder(panel.Slot);
            LocaleString preeditLabel = string.Empty;
            var preedit = preeditUi.Text(preeditLabel, bestFit: true, alignment: Alignment.MiddleLeft);
            preedit.Size.Value = ImeOverlayConfig.PreeditFontSize;
            preedit.Color.Value = colorX.White;
            preedit.RectTransform.AnchorMin.Value = new float2(0f, 0.5f);
            preedit.RectTransform.AnchorMax.Value = new float2(1f, 1f);
            preedit.RectTransform.OffsetMin.Value = new float2(
                ImeOverlayConfig.Padding,
                ImeOverlayConfig.Padding * 0.5f
            );
            preedit.RectTransform.OffsetMax.Value = new float2(
                -ImeOverlayConfig.Padding,
                -ImeOverlayConfig.Padding * 0.5f
            );

            var candidateUi = new UIBuilder(panel.Slot);
            LocaleString candidateLabel = string.Empty;
            var candidates = candidateUi.Text(
                candidateLabel,
                bestFit: true,
                alignment: Alignment.MiddleLeft
            );
            candidates.Size.Value = ImeOverlayConfig.CandidateFontSize;
            candidates.Color.Value = new colorX(0.9f, 0.9f, 0.9f, 1f);
            candidates.RectTransform.AnchorMin.Value = new float2(0f, 0f);
            candidates.RectTransform.AnchorMax.Value = new float2(1f, 0.5f);
            candidates.RectTransform.OffsetMin.Value = new float2(
                ImeOverlayConfig.Padding,
                ImeOverlayConfig.Padding * 0.5f
            );
            candidates.RectTransform.OffsetMax.Value = new float2(
                -ImeOverlayConfig.Padding,
                -ImeOverlayConfig.Padding * 0.5f
            );

            var followSlot = (editor.Text.Target as Component)?.Slot ?? editor.Slot;
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
                FollowSlot = followSlot,
            };

            Subscribe(editor.InputInterface);
            UpdateOnce();
        }
        catch
        {
            // Keep input alive if overlay construction fails.
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
        try
        {
            if (ImeIntegrationOptions.UseTsfFirst && WindowsTsfService.IsAvailable())
            {
                var hwnd = WindowsImeService.GetPreferredWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    WindowsTsfService.EnsureFocusAssociated(hwnd);
                }

                WindowsTsfService.Poll();
            }
        }
        catch
        {
            // TSF polling is best-effort.
        }

        _ = WindowsImeService.TryPlaceNativeUi();
        UpdateOnce();
    }

    private static void UpdateOnce()
    {
        var preeditText = WindowsIme.GetCompositionString();
        var candidateData = WindowsIme.GetCandidateData();
        var candidateText = FormatCandidates(candidateData);
        var showOverlay =
            ImeIntegrationOptions.ForceOverlay
            || (!string.IsNullOrEmpty(preeditText) || !string.IsNullOrEmpty(candidateText));

        foreach (var overlay in Overlays.Values)
        {
            try
            {
                overlay.Preedit.Text = preeditText;
                overlay.Candidates.Text = candidateText;
                overlay.Slot.ActiveSelf = showOverlay;
                UpdateTransform(overlay);
            }
            catch
            {
                // Ignore individual overlay failures.
            }
        }
    }

    private static string FormatCandidates(WindowsImeService.CandidateData? candidateData)
    {
        if (candidateData?.Items is not { Length: > 0 } items)
        {
            return string.Empty;
        }

        var rendered = new string[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            rendered[index] =
                index == candidateData.Selection ? $"[{items[index]}]" : items[index];
        }

        return string.Join("  ", rendered);
    }

    private static void UpdateTransform(Overlay overlay)
    {
        var followSlot = overlay.FollowSlot;
        if (followSlot.IsDestroyed)
        {
            return;
        }

        var scale = followSlot.ActiveUserRoot?.GlobalScale ?? 1f;
        var offset = (followSlot.Up * (ImeOverlayConfig.UpOffset * scale))
            - (followSlot.Forward * (ImeOverlayConfig.ForwardOffset * scale));
        overlay.Slot.GlobalPosition = followSlot.GlobalPosition + offset;
        overlay.Slot.GlobalRotation = followSlot.GlobalRotation;
    }
}
