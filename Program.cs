using System;
using System.Runtime.InteropServices;

class Program
{
    static IntPtr _thumbHandle;
    static IntPtr _windowHandle;

    static void Main()
    {
        string className = "AVSOverlayWindow";

        // Enregistrement de la classe de fenêtre
        WNDCLASS wnd = new WNDCLASS
        {
            lpfnWndProc = WndProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };
        RegisterClass(ref wnd);

        // Recherche de la fenêtre AVS
        IntPtr targetWindow = FindWindow(null, "AVS");
        if (targetWindow == IntPtr.Zero)
        {
            Console.WriteLine("Fenêtre AVS introuvable.");
            return;
        }

        // Taille écran
        int screenWidth = GetSystemMetrics(0);
        int screenHeight = GetSystemMetrics(1);

        // Création de la fenêtre plein écran
        _windowHandle = CreateWindowEx(
            0,
            className,
            "AVS Overlay",
            WS_POPUP,
            0, 0, screenWidth, screenHeight,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero
        );
        ShowWindow(_windowHandle, SW_SHOW);

        // Taille de la fenêtre AVS
        RECT avsRect;
        if (!GetWindowRect(targetWindow, out avsRect))
        {
            Console.WriteLine("Échec de GetWindowRect.");
            return;
        }

        int avsWidth = avsRect.right - avsRect.left;
        int avsHeight = avsRect.bottom - avsRect.top;

        // Enregistrement du thumbnail
        int result = DwmRegisterThumbnail(_windowHandle, targetWindow, out _thumbHandle);
        if (result != 0)
        {
            Console.WriteLine("Échec de DwmRegisterThumbnail.");
            return;
        }

        // Crop manuel
        int cropLeft = 11;
        int cropTop = 20;
        int cropRight = 8;
        int cropBottom = 14;

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

        Console.WriteLine("Miroir AVS actif. Appuie sur ÉCHAP pour quitter.");

        // Boucle de messages
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
        if (msg == WM_KEYDOWN && wParam.ToInt32() == 0x1B) // ESC
            PostQuitMessage(0);

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // === Constantes ===
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
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);
}
