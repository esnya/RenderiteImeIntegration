using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using ResoniteModLoader;

namespace ResoniteImeIntegration.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsTsfService
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("AA80E80C-2021-11D2-93E0-0060B067B86E")]
    private interface ITfThreadMgr
    {
        void Activate(out uint clientId);

        void Deactivate();

        void CreateDocumentMgr([MarshalAs(UnmanagedType.Interface)] out ITfDocumentMgr documentMgr);

        void EnumDocumentMgrs(out nint documentMgrEnumerator);

        void GetFocus([MarshalAs(UnmanagedType.Interface)] out ITfDocumentMgr documentMgrFocus);

        void SetFocus([MarshalAs(UnmanagedType.Interface)] ITfDocumentMgr documentMgrFocus);

        void AssociateFocus(
            nint hwnd,
            [MarshalAs(UnmanagedType.Interface)] ITfDocumentMgr newDocumentMgr,
            [MarshalAs(UnmanagedType.Interface)] out ITfDocumentMgr previousDocumentMgr
        );

        void IsThreadFocus(out bool threadFocus);

        void GetFunctionProvider(ref Guid clsid, out nint functionProvider);

        void EnumFunctionProviders(out nint functionProviderEnumerator);

        void GetGlobalCompartment(out nint compartmentMgr);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("AA80E80D-2021-11D2-93E0-0060B067B86E")]
    private interface ITfDocumentMgr
    {
        void CreateContext(
            uint clientId,
            uint flags,
            [MarshalAs(UnmanagedType.IUnknown)] object? punk,
            [MarshalAs(UnmanagedType.Interface)] out ITfContext context,
            out uint editCookie
        );

        void Push([MarshalAs(UnmanagedType.Interface)] ITfContext context);

        void Pop(out uint editCookie);

        void GetTop([MarshalAs(UnmanagedType.Interface)] out ITfContext context);

        void GetBase([MarshalAs(UnmanagedType.Interface)] out ITfContext context);

        void EnumContexts(out nint contextEnumerator);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("AA80E80F-2021-11D2-93E0-0060B067B86E")]
    private interface ITfContext;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EA1EA134-19DF-11D7-A6D6-00065B84435C")]
    private interface ITfUIElementMgr
    {
        void BeginUIElement([MarshalAs(UnmanagedType.IUnknown)] object element, out uint id, out bool show);

        void UpdateUIElement(uint id);

        void EndUIElement(uint id);

        void GetUIElement(uint id, out ITfUIElement element);

        void EnumUIElements(out nint elementEnumerator);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EA1EA135-19DF-11D7-A6D6-00065B84435C")]
    private interface ITfUIElement;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EA1EA138-19DF-11D7-A6D6-00065B84435C")]
    private interface ITfReadingInformationUIElement
    {
        void GetUpdatedFlags(out uint updatedFlags);

        void GetDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object documentMgr);

        void GetString([MarshalAs(UnmanagedType.BStr)] out string text);

        void GetMaxReadingStringLength(out uint maxLength);

        void GetErrorIndex(out uint errorIndex);

        void IsVertical([MarshalAs(UnmanagedType.Bool)] out bool vertical);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A3AD50FB-9BDB-49e6-90BD-A7FC695A6675")]
    private interface ITfCandidateListUIElement
    {
        void GetUpdatedFlags(out uint updatedFlags);

        void GetDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object documentMgr);

        void GetCount(out int count);

        void GetSelection(out int selection);

        void GetString(int index, [MarshalAs(UnmanagedType.BStr)] out string text);

        void GetPageIndex(nint pageIndex, int size, out int pageCount);

        void SetPageIndex(nint pageIndex, int pageCount);

        void GetCurrentPage(out int currentPage);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EA1EA137-19DF-11D7-A6D6-00065B84435C")]
    private interface IEnumTfUIElements
    {
        void Clone(out IEnumTfUIElements enumerator);

        void Next(uint count, [MarshalAs(UnmanagedType.Interface)] out ITfUIElement element, out uint fetched);

        void Reset();

        void Skip(uint count);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    private static readonly Guid CandidateListUiElementIid =
        new("EA1EA139-19DF-11D7-A6D6-00065B84435C");
    private static readonly Guid ReadingInfoUiElementIid =
        new("EA1EA138-19DF-11D7-A6D6-00065B84435C");

    private static readonly object Sync = new();
    private static bool _initialized;
    private static bool _available;
    private static ITfThreadMgr? _threadMgr;
    private static ITfUIElementMgr? _uiElementMgr;
    private static ITfDocumentMgr? _documentMgr;
    private static ITfContext? _context;
    private static uint _clientId;
    private static uint _editCookie;
    private static nint _associatedHwnd;
    private static Thread? _staThread;
    private static volatile bool _staReady;
    private static WindowsImeService.CandidateData _lastCandidates = new([], -1, 0, 0);
    private static string _lastPreedit = string.Empty;
    private static int _lastUiCount;

    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return _available;
            }

            try
            {
                _ = CoInitializeEx(IntPtr.Zero, 2);
                Type? threadManagerType = Type.GetTypeFromCLSID(
                    new Guid("529A9E6B-6587-4F23-AB9E-9C7D683E3C50")
                );
                _threadMgr = threadManagerType is null
                    ? null
                    : Activator.CreateInstance(threadManagerType) as ITfThreadMgr;
                _threadMgr?.Activate(out _clientId);
                _uiElementMgr = _threadMgr as ITfUIElementMgr;
            }
            catch
            {
                _threadMgr = null;
                _uiElementMgr = null;
            }

            if (_threadMgr is null)
            {
                StartStaThread();
            }

            _available = _threadMgr is not null || _staReady;
            _initialized = _available;
            return _available;
        }
    }

    public static string GetCompositionString() => _lastPreedit;

    public static WindowsImeService.CandidateData? GetCandidateData() =>
        !_available ? null : _lastCandidates;

    public static int GetLastUiCount() => _lastUiCount;

    public static void EnsureFocusAssociated(nint hwnd)
    {
        if (!_available || hwnd == IntPtr.Zero || _threadMgr is null)
        {
            return;
        }

        if (_associatedHwnd == hwnd && _documentMgr is not null)
        {
            return;
        }

        try
        {
            _threadMgr.CreateDocumentMgr(out _documentMgr);
            if (_documentMgr is not null)
            {
                _documentMgr.CreateContext(_clientId, 0, null, out _context, out _editCookie);
                _documentMgr.Push(_context);
                _threadMgr.AssociateFocus(hwnd, _documentMgr, out _);
                _threadMgr.SetFocus(_documentMgr);
                _associatedHwnd = hwnd;
            }
        }
        catch
        {
            // TSF focus association is best-effort.
        }
    }

    public static void DissociateFocus()
    {
        if (!_available || _threadMgr is null || _associatedHwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _threadMgr.AssociateFocus(_associatedHwnd, null!, out _);
        }
        catch
        {
            // Ignore teardown failures.
        }
        finally
        {
            _associatedHwnd = IntPtr.Zero;
        }
    }

    public static void Poll()
    {
        if (!_available || _uiElementMgr is null)
        {
            return;
        }

        nint enumHandle = IntPtr.Zero;
        try
        {
            _uiElementMgr.EnumUIElements(out enumHandle);
            if (enumHandle == IntPtr.Zero)
            {
                _lastUiCount = 0;
                return;
            }

            var enumerator = (IEnumTfUIElements)Marshal.GetObjectForIUnknown(enumHandle);
            _lastUiCount = 0;
            while (true)
            {
                enumerator.Next(1, out var element, out var fetched);
                if (fetched == 0 || element is null)
                {
                    break;
                }

                _lastUiCount++;
                TryUpdateFromElement(element);
            }
        }
        catch
        {
            // TSF polling is best-effort.
        }
        finally
        {
            if (enumHandle != IntPtr.Zero)
            {
                Marshal.Release(enumHandle);
            }
        }
    }

    public static void Teardown()
    {
        _uiElementMgr = null;
        _documentMgr = null;
        _context = null;
        _associatedHwnd = IntPtr.Zero;
        _lastUiCount = 0;
        _lastPreedit = string.Empty;
        _lastCandidates = new([], -1, 0, 0);
        _initialized = false;
        _available = false;
    }

    private static void StartStaThread()
    {
        if (_staThread is not null)
        {
            return;
        }

        _staThread = new Thread(() =>
        {
            try
            {
                _ = CoInitializeEx(IntPtr.Zero, 2);
                Type? threadManagerType = Type.GetTypeFromCLSID(
                    new Guid("529A9E6B-6587-4F23-AB9E-9C7D683E3C50")
                );
                _threadMgr = threadManagerType is null
                    ? null
                    : Activator.CreateInstance(threadManagerType) as ITfThreadMgr;
                _threadMgr?.Activate(out _clientId);
                _uiElementMgr = _threadMgr as ITfUIElementMgr;
                _staReady = _threadMgr is not null;
            }
            catch
            {
                _staReady = false;
                return;
            }

            while (true)
            {
                try
                {
                    while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
                    {
                        _ = TranslateMessage(ref message);
                        _ = DispatchMessage(ref message);
                    }

                    Thread.Sleep(5);
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        })
        {
            IsBackground = true,
            Name = "ResoniteImeIntegration.TSF.STA",
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
    }

    private static void TryUpdateFromElement(ITfUIElement element)
    {
        nint unknown = IntPtr.Zero;
        nint candidatePtr = IntPtr.Zero;
        nint readingPtr = IntPtr.Zero;

        try
        {
            unknown = Marshal.GetIUnknownForObject(element);
            if (
                Marshal.QueryInterface(unknown, in CandidateListUiElementIid, out candidatePtr) == 0
                && candidatePtr != IntPtr.Zero
            )
            {
                var candidateUi = (ITfCandidateListUIElement)
                    Marshal.GetObjectForIUnknown(candidatePtr);
                candidateUi.GetCount(out var count);
                candidateUi.GetSelection(out var selection);
                var items = new string[Math.Max(0, count)];
                for (var index = 0; index < items.Length; index++)
                {
                    try
                    {
                        candidateUi.GetString(index, out items[index]);
                    }
                    catch
                    {
                        items[index] = string.Empty;
                    }
                }

                _lastCandidates = new WindowsImeService.CandidateData(items, selection, 0, 0);
            }

            if (
                Marshal.QueryInterface(unknown, in ReadingInfoUiElementIid, out readingPtr) == 0
                && readingPtr != IntPtr.Zero
            )
            {
                var readingUi = (ITfReadingInformationUIElement)
                    Marshal.GetObjectForIUnknown(readingPtr);
                readingUi.GetString(out _lastPreedit);
            }
        }
        catch (Exception ex)
        {
            if (Core.ImeIntegrationOptions.VerboseLogging)
            {
                ResoniteMod.Msg($"TSF UI update failed: {ex.Message}");
            }
        }
        finally
        {
            if (candidatePtr != IntPtr.Zero)
            {
                Marshal.Release(candidatePtr);
            }

            if (readingPtr != IntPtr.Zero)
            {
                Marshal.Release(readingPtr);
            }

            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint reserved, uint initMode);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PeekMessage(
        out Msg message,
        nint hWnd,
        uint filterMin,
        uint filterMax,
        uint remove
    );

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DispatchMessage(ref Msg message);
}
