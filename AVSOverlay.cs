using System.Runtime.InteropServices;
using System.Text;

class AVSOverlay
{
    static IntPtr _thumbHandle;
    static IntPtr _windowHandle;
    static IntPtr _targetWindow;
    static Thread _watchThread;

    static void Main(string[] args)
    {
        // Default crop values
        int cropLeft = 11;
        int cropTop = 20;
        int cropRight = 8;
        int cropBottom = 14;
        int monitorIndex = 0;

        // CLI options
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg == "--help")
            {
                Console.WriteLine("Available options:");
                Console.WriteLine("  --crop L,T,R,B   Set crop margins (left, top, right, bottom)");
                Console.WriteLine("  --monitor N      Set target monitor index (0, 1, 2...)");
                Console.WriteLine("  --help           Show this help message");
                return;
            }
            else if (arg == "--crop" && i + 1 < args.Length)
            {
                var parts = args[i + 1].Split(',');
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out cropLeft) &&
                    int.TryParse(parts[1], out cropTop) &&
                    int.TryParse(parts[2], out cropRight) &&
                    int.TryParse(parts[3], out cropBottom))
                {
                    Console.WriteLine($"Using custom crop: {cropLeft},{cropTop},{cropRight},{cropBottom}");
                    i++;
                }
                else
                {
                    Console.WriteLine("Invalid format for --crop. Use: --crop 8,30,8,8");
                    return;
                }
            }
            else if (arg == "--monitor" && i + 1 < args.Length && int.TryParse(args[i + 1], out monitorIndex))
            {
                Console.WriteLine($"Selected monitor: {monitorIndex}");
                i++;
            }
        }

        string className = "AVSOverlayWindow";

        // Register custom window class
        WNDCLASS wnd = new WNDCLASS
        {
            lpfnWndProc = WndProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };
        RegisterClass(ref wnd);

        // Wait for a valid AVS window belonging to winamp.exe
        Console.WriteLine("Waiting for AVS window...");
        while ((_targetWindow = FindValidAVSWindow()) == IntPtr.Zero)
        {
            Thread.Sleep(1000);
        }

        Console.WriteLine("AVS window detected.");

        // Monitor selection
        var monitors = GetMonitorRects();
        if (monitorIndex >= monitors.Count)
        {
            Console.WriteLine($"Monitor {monitorIndex} not found. {monitors.Count} detected.");
            return;
        }

        RECT bounds = monitors[monitorIndex];

        // Create fullscreen borderless window
        int width = bounds.right - bounds.left;
        int height = bounds.bottom - bounds.top;

        _windowHandle = CreateWindowEx(
            0,
            className,
            "AVS Overlay",
            WS_POPUP,
            bounds.left, bounds.top, width, height,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero
        );

        ShowWindow(_windowHandle, SW_SHOW);

        void RegisterThumbnail()
        {
            if (!GetWindowRect(_targetWindow, out RECT avsRect))
            {
                Console.WriteLine("Failed to get AVS window size.");
                return;
            }

            int avsWidth = avsRect.right - avsRect.left;
            int avsHeight = avsRect.bottom - avsRect.top;

            int result = DwmRegisterThumbnail(_windowHandle, _targetWindow, out _thumbHandle);
            if (result != 0)
            {
                Console.WriteLine("Failed to register thumbnail.");
                return;
            }

            int screenWidth = bounds.right - bounds.left;
            int screenHeight = bounds.bottom - bounds.top;

            DWM_THUMBNAIL_PROPERTIES props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTSOURCE,
                rcDestination = new RECT
                {
                    left = 0,
                    top = 0,
                    right = screenWidth,
                    bottom = screenHeight
                },
                rcSource = new RECT
                {
                    left = cropLeft,
                    top = cropTop,
                    right = avsWidth - cropRight,
                    bottom = avsHeight - cropBottom
                },
                opacity = 255,
                fVisible = true,
                fSourceClientAreaOnly = false
            };

            DwmUpdateThumbnailProperties(_thumbHandle, ref props);
        }

        RegisterThumbnail();

        // Monitor AVS window status in a background thread
        _watchThread = new Thread(() =>
        {
            while (true)
            {
                if (!IsWindow(_targetWindow))
                {
                    Console.WriteLine("AVS window closed. Waiting for it to return...");
                    DwmUnregisterThumbnail(_thumbHandle);

                    while ((_targetWindow = FindValidAVSWindow()) == IntPtr.Zero)
                        Thread.Sleep(1000);

                    Console.WriteLine("AVS window found, reconfiguring thumbnail...");
                    RegisterThumbnail();
                }
                Thread.Sleep(1000);
            }
        });
        _watchThread.IsBackground = true;
        _watchThread.Start();

        Console.WriteLine("AVS mirroring active.\nClick the overlay monitor then press ESC to quit.");

        // Message loop
        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_thumbHandle != IntPtr.Zero)
            DwmUnregisterThumbnail(_thumbHandle);
    }

    // Get all monitors bounds
    static List<RECT> GetMonitorRects()
    {
        List<RECT> monitors = new List<RECT>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFO info = new MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                if (GetMonitorInfo(hMonitor, ref info))
                {
                    monitors.Add(info.rcMonitor);
                }

                return true;
            }, IntPtr.Zero);

        return monitors;
    }


    // Scan all windows and return the first AVS window belonging to winamp.exe
    static IntPtr FindValidAVSWindow()
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            StringBuilder title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);

            if (title.ToString() == "AVS" && IsWindowFromWinamp(hWnd))
            {
                found = hWnd;
                return false; // stop
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    // Validate that the window belongs to winamp.exe
    static bool IsWindowFromWinamp(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        if (hProcess == IntPtr.Zero) return false;

        var path = new StringBuilder(1024);
        GetModuleFileNameEx(hProcess, IntPtr.Zero, path, path.Capacity);
        string exePath = path.ToString().ToLower();

        return exePath.EndsWith("winamp.exe");
    }

    static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_KEYDOWN && wParam.ToInt32() == 0x1B)
            PostQuitMessage(0);

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // === Constants ===
    const int WS_POPUP = unchecked((int)0x80000000);
    const int SW_SHOW = 5;
    const uint WM_KEYDOWN = 0x0100;
    const int DWM_TNP_RECTDESTINATION = 0x00000001;
    const int DWM_TNP_RECTSOURCE = 0x00000002;
    const int DWM_TNP_OPACITY = 0x00000004;
    const int DWM_TNP_VISIBLE = 0x00000008;
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint PROCESS_VM_READ = 0x0010;

    // === Structs ===
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)]
    struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
        [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct WNDCLASS
    {
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    // === Win32 API ===
    [DllImport("user32.dll")] static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpmsg);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
    [DllImport("psapi.dll", SetLastError = true)] static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, int nSize);
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
