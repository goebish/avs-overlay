using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Windows.Forms; // Required for Screen[]
using System.Threading;

class Program
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

        // Handle command-line arguments
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

        // Register the window class
        WNDCLASS wnd = new WNDCLASS
        {
            lpfnWndProc = WndProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };
        RegisterClass(ref wnd);

        // Wait for the AVS window at startup
        Console.WriteLine("Waiting for AVS window...");
        while ((_targetWindow = FindWindow(null, "AVS")) == IntPtr.Zero)
        {
            Console.WriteLine("AVS window not found, retrying in 1 second...");
            Thread.Sleep(1000);
        }

        Console.WriteLine("AVS window detected.");

        // Monitor selection
        var screens = Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length)
        {
            Console.WriteLine($"Monitor {monitorIndex} not found. {screens.Length} detected.");
            return;
        }
        var screen = screens[monitorIndex];
        var bounds = screen.Bounds;

        // Create fullscreen window on selected screen
        _windowHandle = CreateWindowEx(
            0,
            className,
            "AVS Overlay",
            WS_POPUP,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero
        );
        ShowWindow(_windowHandle, SW_SHOW);

        void RegisterThumbnail()
        {
            // Get AVS window size
            if (!GetWindowRect(_targetWindow, out RECT avsRect))
            {
                Console.WriteLine("Failed to get AVS window size.");
                return;
            }

            int avsWidth = avsRect.right - avsRect.left;
            int avsHeight = avsRect.bottom - avsRect.top;

            // Register the thumbnail
            int result = DwmRegisterThumbnail(_windowHandle, _targetWindow, out _thumbHandle);
            if (result != 0)
            {
                Console.WriteLine("Failed to register thumbnail.");
                return;
            }

            DWM_THUMBNAIL_PROPERTIES props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTSOURCE,
                rcDestination = new RECT
                {
                    left = 0,
                    top = 0,
                    right = bounds.Width,
                    bottom = bounds.Height
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

        // Start thread to monitor AVS window and reconnect if needed
        _watchThread = new Thread(() =>
        {
            while (true)
            {
                if (!IsWindow(_targetWindow))
                {
                    Console.WriteLine("AVS window closed. Waiting for it to return...");
                    DwmUnregisterThumbnail(_thumbHandle);

                    while ((_targetWindow = FindWindow(null, "AVS")) == IntPtr.Zero)
                        Thread.Sleep(1000);

                    Console.WriteLine("AVS window found again, reconfiguring thumbnail...");
                    RegisterThumbnail();
                }
                Thread.Sleep(1000);
            }
        });
        _watchThread.IsBackground = true;
        _watchThread.Start();

        Console.WriteLine("AVS mirroring active. Press ESC to quit.");

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

    // === Structures ===
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

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
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

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

    // === Win32 Interop ===
    [DllImport("user32.dll")] static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpmsg);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);
}
