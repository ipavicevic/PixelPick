using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.System;

namespace PixelPick;

public sealed partial class MainWindow : Window
{
    private const string FreezeText = "Ctrl + / to freeze...";
    private const string UnfreezeText = "Ctrl + / to unfreeze...";
    private const int ZoomPanelSize = 110;
    private const int ZoomScale = 10;
    private const int ZoomCaptureSize = ZoomPanelSize / ZoomScale; // 11 - 11x10 fills panel exactly

    private bool _capturing = true;
    private bool _ctrl = false;
    private Color _currentColor = Colors.White;
    private Color _originalColor = Colors.White;

    private IntPtr _hMouseHook;
    private IntPtr _hKeyboardHook;
    private NativeMethods.HookProcDelegate? _mouseHook;
    private NativeMethods.HookProcDelegate? _keyboardHook;

    // Captured screen pixels for zoom panel
    private byte[]? _zoomPixels;
    private readonly object _zoomLock = new();

    public MainWindow()
    {
        InitializeComponent();
        SetupWindow();
        SetupHooks();
    }

    private void SetupWindow()
    {
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = "PixelPick";
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"));

        bool sized = false;
        Activated += (s, e) =>
        {
            if (sized) return;
            sized = true;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            float dpiScale = NativeMethods.GetDpiForWindow(hwnd) / 96f;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)Math.Ceiling(RootGrid.Width * dpiScale),
                (int)Math.Ceiling(RootGrid.Height * dpiScale) + AppWindow.TitleBar.Height));
        };
    }

    private void SetupHooks()
    {
        IntPtr user32 = NativeMethods.LoadLibrary("user32.dll");

        _mouseHook = MouseHookProc;
        _hMouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHook, user32, 0);

        _keyboardHook = KeyboardHookProc;
        _hKeyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardHook, user32, 0);
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (_capturing)
        {
            var hs = (NativeMethods.MOUSEHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MOUSEHOOKSTRUCT))!;
            CaptureAt(hs.pt.x, hs.pt.y);
        }
        return NativeMethods.CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if ((int)wParam == NativeMethods.WM_KEYDOWN)
        {
            var ki = (NativeMethods.KEYBOARDHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KEYBOARDHOOKSTRUCT))!;
            if (ki.vkCode == NativeMethods.VK_CONTROL ||
                ki.vkCode == NativeMethods.VK_LCONTROL ||
                ki.vkCode == NativeMethods.VK_RCONTROL)
            {
                _ctrl = true;
            }
            else if (ki.vkCode == NativeMethods.VK_OEM_SLASH && _ctrl)
            {
                _capturing = !_capturing;
                DispatcherQueue.TryEnqueue(() =>
                {
                    EditButton.IsEnabled = !_capturing;
                    RevertButton.IsEnabled = !_capturing;
                    StatusText.Text = _capturing ? FreezeText : UnfreezeText;
                });
            }
        }
        else if ((int)wParam == NativeMethods.WM_KEYUP)
        {
            var ki = (NativeMethods.KEYBOARDHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KEYBOARDHOOKSTRUCT))!;
            if (ki.vkCode == NativeMethods.VK_CONTROL ||
                ki.vkCode == NativeMethods.VK_LCONTROL ||
                ki.vkCode == NativeMethods.VK_RCONTROL)
                _ctrl = false;
        }
        return NativeMethods.CallNextHookEx(_hKeyboardHook, nCode, wParam, lParam);
    }

    private void CaptureAt(int x, int y)
    {
        int half = ZoomCaptureSize / 2;
        int left = x - half;
        int top = y - half;

        // Capture ZoomCaptureSize x ZoomCaptureSize pixels via GDI BitBlt
        IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(screenDC, ZoomCaptureSize, ZoomCaptureSize);
        IntPtr oldBitmap = NativeMethods.SelectObject(memDC, hBitmap);

        NativeMethods.BitBlt(memDC, 0, 0, ZoomCaptureSize, ZoomCaptureSize, screenDC, left, top, NativeMethods.SRCCOPY);

        // Extract pixels as BGRA bytes
        var bmi = new NativeMethods.BITMAPINFO();
        bmi.bmiHeader.biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = ZoomCaptureSize;
        bmi.bmiHeader.biHeight = -ZoomCaptureSize; // top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0; // BI_RGB

        byte[] pixels = new byte[ZoomCaptureSize * ZoomCaptureSize * 4];
        NativeMethods.GetDIBits(memDC, hBitmap, 0, (uint)ZoomCaptureSize, pixels, ref bmi, 0);

        NativeMethods.SelectObject(memDC, oldBitmap);
        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memDC);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);

        // Center pixel (BGRA)
        int centerIdx = (ZoomCaptureSize / 2 * ZoomCaptureSize + ZoomCaptureSize / 2) * 4;
        byte b = pixels[centerIdx];
        byte g = pixels[centerIdx + 1];
        byte r = pixels[centerIdx + 2];
        Color color = Color.FromArgb(255, r, g, b);

        lock (_zoomLock)
            _zoomPixels = pixels;

        _originalColor = color;

        DispatcherQueue.TryEnqueue(() => SetColor(color));
    }

    private void SetColor(Color color)
    {
        _currentColor = color;
        HexValue.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        RgbValue.Text = $"{color.R,3}, {color.G,3}, {color.B,3}";

        var (h, s, l) = RgbToHsl(color.R, color.G, color.B);
        HslValue.Text = $"{h}°, {s}%, {l}%";

        ColorSampleCanvas.Invalidate();
        ZoomCanvas.Invalidate();
    }

    private static (int h, int s, int l) RgbToHsl(byte r, byte g, byte b)
    {
        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float l = (max + min) / 2f;
        float s = 0, h = 0;
        float delta = max - min;

        if (delta > 0)
        {
            s = delta / (1f - Math.Abs(2f * l - 1f));
            if (max == rf) h = 60f * (((gf - bf) / delta) % 6f);
            else if (max == gf) h = 60f * ((bf - rf) / delta + 2f);
            else h = 60f * ((rf - gf) / delta + 4f);
            if (h < 0) h += 360f;
        }

        return ((int)Math.Round(h), (int)Math.Round(s * 100), (int)Math.Round(l * 100));
    }

    private void ColorSampleCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        float half = (float)sender.ActualWidth / 2;
        args.DrawingSession.FillRectangle(0, 0, half, (float)sender.ActualHeight,
            new Microsoft.Graphics.Canvas.Brushes.CanvasSolidColorBrush(sender, _originalColor));
        args.DrawingSession.FillRectangle(half, 0, half, (float)sender.ActualHeight,
            new Microsoft.Graphics.Canvas.Brushes.CanvasSolidColorBrush(sender, _currentColor));
    }

    private void ZoomCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        byte[]? pixels;
        lock (_zoomLock)
            pixels = _zoomPixels;

        if (pixels == null) return;

        var ds = args.DrawingSession;
        ds.Antialiasing = Microsoft.Graphics.Canvas.CanvasAntialiasing.Aliased;
        float w = (float)sender.ActualWidth;
        float h = (float)sender.ActualHeight;
        float scale = Math.Min(w, h) / ZoomCaptureSize;
        float panelCx = w / 2.0f;
        float panelCy = h / 2.0f;
        float imgX = panelCx - (ZoomCaptureSize / 2.0f) * scale;
        float imgY = panelCy - (ZoomCaptureSize / 2.0f) * scale;

        // Draw each captured pixel as a scaled rectangle, snapping to integer boundaries
        for (int py = 0; py < ZoomCaptureSize; py++)
        {
            for (int px = 0; px < ZoomCaptureSize; px++)
            {
                int idx = (py * ZoomCaptureSize + px) * 4;
                byte pb = pixels[idx];
                byte pg = pixels[idx + 1];
                byte pr = pixels[idx + 2];
                var c = Color.FromArgb(255, pr, pg, pb);
                float x0 = MathF.Round(imgX + px * scale);
                float y0 = MathF.Round(imgY + py * scale);
                float x1 = MathF.Round(imgX + (px + 1) * scale);
                float y1 = MathF.Round(imgY + (py + 1) * scale);
                ds.FillRectangle(x0, y0, x1 - x0, y1 - y0,
                    new Microsoft.Graphics.Canvas.Brushes.CanvasSolidColorBrush(sender, c));
            }
        }

        // Crosshair — derive from same snapped grid as pixel drawing
        int centerIdx = (ZoomCaptureSize / 2 * ZoomCaptureSize + ZoomCaptureSize / 2) * 4;
        byte cb = pixels[centerIdx], cg = pixels[centerIdx + 1], cr = pixels[centerIdx + 2];
        float brightness = (0.299f * cr + 0.587f * cg + 0.114f * cb) / 255f;
        Color penColor = brightness > 0.5f ? Colors.Black : Colors.White;

        int half = ZoomCaptureSize / 2;
        float cx0 = MathF.Round(imgX + half * scale);
        float cy0 = MathF.Round(imgY + half * scale);
        float cx1 = MathF.Round(imgX + (half + 1) * scale);
        float cy1 = MathF.Round(imgY + (half + 1) * scale);
        float lineX = MathF.Round((cx0 + cx1) / 2f);
        float lineY = MathF.Round((cy0 + cy1) / 2f);

        ds.DrawLine(0, lineY, cx0, lineY, penColor);
        ds.DrawLine(cx1, lineY, w, lineY, penColor);
        ds.DrawLine(lineX, 0, lineX, cy0, penColor);
        ds.DrawLine(lineX, cy1, lineX, h, penColor);
        ds.DrawRectangle(cx0, cy0, cx1 - cx0, cy1 - cy0, penColor);
    }

    private async void CopyToClipboard(TextBox box, string value)
    {
        var dp = new DataPackage();
        dp.SetText(value);
        Clipboard.SetContent(dp);
        string original = box.Text;
        box.Text = "Copied!";
        await Task.Delay(800);
        box.Text = original;
    }

    private void CopyHexButton_Click(object sender, RoutedEventArgs e) => CopyToClipboard(HexValue, HexValue.Text);
    private void CopyRgbButton_Click(object sender, RoutedEventArgs e) => CopyToClipboard(RgbValue, RgbValue.Text.Trim());
    private void CopyHslButton_Click(object sender, RoutedEventArgs e) => CopyToClipboard(HslValue, HslValue.Text.Trim());

    private static uint[] _customColors = new uint[16];

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        uint initColor = (uint)(_currentColor.R | (_currentColor.G << 8) | (_currentColor.B << 16));

        var custHandle = GCHandle.Alloc(_customColors, GCHandleType.Pinned);
        try
        {
            var cc = new NativeMethods.CHOOSECOLOR
            {
                lStructSize  = Marshal.SizeOf<NativeMethods.CHOOSECOLOR>(),
                hwndOwner    = hwnd,
                rgbResult    = initColor,
                lpCustColors = custHandle.AddrOfPinnedObject(),
                Flags        = NativeMethods.CC_FULLOPEN | NativeMethods.CC_RGBINIT
            };

            if (NativeMethods.ChooseColor(ref cc))
            {
                byte r = (byte)(cc.rgbResult & 0xFF);
                byte g = (byte)((cc.rgbResult >> 8) & 0xFF);
                byte b = (byte)((cc.rgbResult >> 16) & 0xFF);
                SetColor(Color.FromArgb(255, r, g, b));
            }
        }
        finally
        {
            custHandle.Free();
        }
    }

    private void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        SetColor(_originalColor);
    }
}
