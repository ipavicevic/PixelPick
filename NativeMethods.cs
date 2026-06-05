using System;
using System.Runtime.InteropServices;

namespace PixelPick;

internal static class NativeMethods
{
    public const int WH_MOUSE_LL = 14;
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int VK_CONTROL  = 0x11;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_OEM_SLASH = 0xBF;
    public const int VK_SPACE = 0x20;
    public const int VK_LEFT  = 0x25;
    public const int VK_UP    = 0x26;
    public const int VK_RIGHT = 0x27;
    public const int VK_DOWN  = 0x28;
    public const uint SRCCOPY = 0x00CC0020;

    public delegate IntPtr HookProcDelegate(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, HookProcDelegate lpfn, IntPtr hMod, int dwThreadId);
    [DllImport("user32.dll")] public static extern int UnhookWindowsHookEx(IntPtr idHook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern IntPtr LoadLibrary(string lpFileName);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, uint dwRop);
    [DllImport("gdi32.dll")] public static extern int GetDIBits(IntPtr hDC, IntPtr hBitmap, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEHOOKSTRUCT { public POINT pt; public IntPtr hwnd; public uint wHitTestCode; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBOARDHOOKSTRUCT { public int vkCode; public int scanCode; public int flags; public int time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] bmiColors; }

    // Classic Windows color picker dialog
    public const uint CC_FULLOPEN = 0x0002;
    public const uint CC_RGBINIT  = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct CHOOSECOLOR
    {
        public int    lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public uint   rgbResult;
        public IntPtr lpCustColors;
        public uint   Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll")] public static extern bool ChooseColor(ref CHOOSECOLOR cc);
}
