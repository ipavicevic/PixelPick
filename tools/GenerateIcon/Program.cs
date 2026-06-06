using SkiaSharp;
using Svg.Skia;

var root = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
var svgPath = Path.Combine(root, "Assets", "icon.svg");
var assetsDir = Path.Combine(root, "Assets");

SKBitmap Render(int size)
{
    using var svg = new SKSvg();
    svg.Load(svgPath);
    var pic = svg.Picture!;

    var bmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);

    var src = pic.CullRect;
    float scale = size / Math.Max(src.Width, src.Height);
    canvas.Save();
    canvas.Scale(scale, scale);
    canvas.DrawPicture(pic);
    canvas.Restore();
    canvas.Flush();
    return bmp;
}

void SavePng(SKBitmap bmp, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var img  = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    using var fs   = File.OpenWrite(path);
    data.SaveTo(fs);
    Console.WriteLine($"Written: {Path.GetRelativePath(root, path)}");
}

void SaveIco(int[] sizes, string path)
{
    var pngs = sizes.Select(s =>
    {
        using var bmp = Render(s);
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }).ToArray();

    using var fs = File.OpenWrite(path);
    using var w  = new BinaryWriter(fs);

    w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);

    int offset = 6 + sizes.Length * 16;
    for (int i = 0; i < sizes.Length; i++)
    {
        int dim = sizes[i] >= 256 ? 0 : sizes[i];
        w.Write((byte)dim); w.Write((byte)dim);
        w.Write((byte)0);   w.Write((byte)0);
        w.Write((short)1);  w.Write((short)32);
        w.Write(pngs[i].Length);
        w.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var png in pngs) w.Write(png);
    Console.WriteLine($"Written: {Path.GetRelativePath(root, path)}");
}

// ICO
SaveIco([16, 32, 48, 256], Path.Combine(assetsDir, "icon.ico"));

// Store PNGs
int[] storeSizes = [44, 150, 300, 620];
string[] storeNames = ["Square44x44Logo.png", "Square150x150Logo.png", "StoreLogo.png", "SplashScreen.png"];
for (int i = 0; i < storeSizes.Length; i++)
{
    using var bmp = Render(storeSizes[i]);
    SavePng(bmp, Path.Combine(assetsDir, storeNames[i]));
}
