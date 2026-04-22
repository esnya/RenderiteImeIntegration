using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Text;
using FrooxEngine;

namespace ResoniteImeIntegration.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsImeService
{
    public sealed record CandidateData(string[] Items, int Selection, int PageStart, int PageSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int CbSize;
        public int Flags;
        public nint HwndActive;
        public nint HwndFocus;
        public nint HwndCapture;
        public nint HwndMenuOwner;
        public nint HwndMoveSize;
        public nint HwndCaret;
        public Rect RcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionForm
    {
        public int DwStyle;
        public Point CurrentPos;
        public Rect Area;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CandidateForm
    {
        public int DwIndex;
        public int DwStyle;
        public Point CurrentPos;
        public Rect Area;
    }

    private const int GcsCompStr = 8;
    private const int CfsForcePosition = 0x0020;
    private const int CfsCandidatePos = 0x0040;

    public static string GetCompositionString()
    {
        try
        {
            if (!TryGetImmContext(out var hwnd, out var himc))
            {
                return string.Empty;
            }

            try
            {
                var length = ImmGetCompositionStringW(himc, GcsCompStr, IntPtr.Zero, 0);
                if (length <= 0)
                {
                    return string.Empty;
                }

                var buffer = new byte[length];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    _ = ImmGetCompositionStringW(
                        himc,
                        GcsCompStr,
                        handle.AddrOfPinnedObject(),
                        length
                    );
                }
                finally
                {
                    handle.Free();
                }

                return Encoding.Unicode.GetString(buffer);
            }
            finally
            {
                _ = ImmReleaseContext(hwnd, himc);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    public static CandidateData? GetCandidateData()
    {
        try
        {
            if (!TryGetImmContext(out var hwnd, out var himc))
            {
                return null;
            }

            try
            {
                var length = ImmGetCandidateListW(himc, 0, IntPtr.Zero, 0);
                if (length <= 0)
                {
                    return new CandidateData([], -1, 0, 0);
                }

                var buffer = new byte[length];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var result = ImmGetCandidateListW(himc, 0, handle.AddrOfPinnedObject(), length);
                    return result <= 0 ? new CandidateData([], -1, 0, 0) : ParseCandidateList(buffer);
                }
                finally
                {
                    handle.Free();
                }
            }
            finally
            {
                _ = ImmReleaseContext(hwnd, himc);
            }
        }
        catch
        {
            return null;
        }
    }

    public static bool TryPlaceNativeUi()
    {
        try
        {
            if (!TryGetImmContext(out var hwnd, out var himc))
            {
                return false;
            }

            try
            {
                if (!GetWindowRect(hwnd, out var rect))
                {
                    return false;
                }

                var anchor = new Point
                {
                    X = rect.Left + 48,
                    Y = rect.Top + 96,
                };
                var composition = new CompositionForm
                {
                    DwStyle = CfsForcePosition,
                    CurrentPos = anchor,
                };
                var candidate = new CandidateForm
                {
                    DwIndex = 0,
                    DwStyle = CfsCandidatePos,
                    CurrentPos = anchor,
                };

                return ImmSetCompositionWindow(himc, ref composition)
                    || ImmSetCandidateWindow(himc, ref candidate);
            }
            finally
            {
                _ = ImmReleaseContext(hwnd, himc);
            }
        }
        catch
        {
            return false;
        }
    }

    public static CandidateData ParseCandidateList(byte[] data)
    {
        if (data.Length < 24)
        {
            return new CandidateData([], -1, 0, 0);
        }

        var count = BitConverter.ToInt32(data, 8);
        var selection = BitConverter.ToInt32(data, 12);
        var pageStart = BitConverter.ToInt32(data, 16);
        var pageSize = BitConverter.ToInt32(data, 20);
        if (count <= 0 || data.Length < 24 + (4 * count))
        {
            return new CandidateData([], -1, 0, 0);
        }

        var items = new string[count];
        for (var index = 0; index < count; index++)
        {
            var offset = BitConverter.ToInt32(data, 24 + (4 * index));
            if (offset < 0 || offset >= data.Length)
            {
                items[index] = string.Empty;
                continue;
            }

            var end = offset;
            while (end + 1 < data.Length && (data[end] != 0 || data[end + 1] != 0))
            {
                end += 2;
            }

            items[index] = Encoding.Unicode.GetString(data, offset, Math.Max(0, end - offset));
        }

        return new CandidateData(items, selection, pageStart, pageSize);
    }

    public static nint GetPreferredWindowHandle()
    {
        var engineHandle = TryGetEngineRendererWindowHandle();
        if (engineHandle != IntPtr.Zero)
        {
            return engineHandle;
        }

        return WindowsHwndProvider.TryGetHwndViaRenderite(out var reflectHandle)
            ? reflectHandle
            : TryGetImeWindowHandle();
    }

    private static bool TryGetImmContext(out nint hwnd, out nint himc)
    {
        hwnd = GetPreferredWindowHandle();
        if (hwnd == IntPtr.Zero)
        {
            hwnd = TryGetImeWindowHandle();
        }

        himc = hwnd == IntPtr.Zero ? IntPtr.Zero : ImmGetContext(hwnd);
        if (himc != IntPtr.Zero)
        {
            return true;
        }

        himc = TryGetHimc(out var fallbackHwnd);
        if (himc == IntPtr.Zero)
        {
            hwnd = IntPtr.Zero;
            return false;
        }

        hwnd = fallbackHwnd;
        return true;
    }

    private static nint TryGetImeWindowHandle()
    {
        try
        {
            var foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                var threadId = GetWindowThreadProcessId(foreground, out _);
                var gui = new GuiThreadInfo { CbSize = Marshal.SizeOf<GuiThreadInfo>() };
                if (GetGUIThreadInfo(threadId, ref gui))
                {
                    if (gui.HwndCaret != IntPtr.Zero)
                    {
                        return gui.HwndCaret;
                    }

                    if (gui.HwndFocus != IntPtr.Zero)
                    {
                        return gui.HwndFocus;
                    }

                    if (gui.HwndActive != IntPtr.Zero)
                    {
                        return gui.HwndActive;
                    }
                }

                var currentThread = GetCurrentThreadId();
                if (AttachThreadInput(currentThread, threadId, true))
                {
                    try
                    {
                        var focus = GetFocus();
                        if (focus != IntPtr.Zero)
                        {
                            return focus;
                        }
                    }
                    finally
                    {
                        _ = AttachThreadInput(currentThread, threadId, false);
                    }
                }

                return foreground;
            }
        }
        catch
        {
            // Fall through to process main window.
        }

        try
        {
            return Process.GetCurrentProcess().MainWindowHandle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static nint TryGetHimc(out nint hwndUsed)
    {
        hwndUsed = IntPtr.Zero;

        try
        {
            var foreground = GetForegroundWindow();
            var foregroundThread = foreground != IntPtr.Zero
                ? GetWindowThreadProcessId(foreground, out _)
                : 0u;

            var gui = new GuiThreadInfo { CbSize = Marshal.SizeOf<GuiThreadInfo>() };
            var candidates =
                foregroundThread != 0 && GetGUIThreadInfo(foregroundThread, ref gui)
                    ? new[] { gui.HwndCaret, gui.HwndFocus, gui.HwndActive, foreground }
                    : new[] { foreground, GetActiveWindow() };

            foreach (var candidate in candidates)
            {
                if (candidate == IntPtr.Zero)
                {
                    continue;
                }

                var himc = ImmGetContext(candidate);
                if (himc != IntPtr.Zero)
                {
                    hwndUsed = candidate;
                    return himc;
                }
            }
        }
        catch
        {
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private static nint TryGetEngineRendererWindowHandle()
    {
        try
        {
            return Engine.Current?.RenderSystem?.RendererWindowHandle ?? IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo guiThreadInfo);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern nint GetFocus();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out Rect rect);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint hWnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionStringW(
        nint himc,
        int index,
        nint buffer,
        int bufferLength
    );

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCandidateListW(
        nint himc,
        uint candidateIndex,
        nint candidateList,
        int bufferLength
    );

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(nint hWnd, nint himc);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("imm32.dll")]
    private static extern bool ImmSetCompositionWindow(nint himc, ref CompositionForm compositionForm);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("imm32.dll")]
    private static extern bool ImmSetCandidateWindow(nint himc, ref CandidateForm candidateForm);
}
