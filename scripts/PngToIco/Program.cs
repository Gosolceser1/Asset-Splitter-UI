// Converts a PNG to a multi-size ICO with BMP-encoded frames (Skia/Avalonia compatible).
// Usage: dotnet run -- <input.png> <output.ico>

using SkiaSharp;

string pngPath = args.Length > 0 ? args[0] : "app-icon.png";
string icoPath = args.Length > 1 ? args[1] : "app-icon.ico";

int[] sizes = [256, 48, 32, 16];

using var srcStream = File.OpenRead(pngPath);
using var original  = SKBitmap.Decode(srcStream) ?? throw new Exception("Cannot decode PNG");

// Render each size as a 32bpp BMP (no file header — ICO uses DIB format)
var frames = sizes.Select(sz =>
{
    using var scaled = new SKBitmap(sz, sz, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(scaled);
    canvas.Clear(SKColors.Transparent);
    var src  = new SKRect(0, 0, original.Width, original.Height);
    var dst  = new SKRect(0, 0, sz, sz);
    canvas.DrawBitmap(original, src, dst, new SKPaint { FilterQuality = SKFilterQuality.High });
    canvas.Flush();

    // Build DIB (BITMAPINFOHEADER + pixels, rows bottom-up, AND mask)
    return BuildDib(scaled);
}).ToArray();

// Write ICO
using var out_ = File.Create(icoPath);
using var bw   = new BinaryWriter(out_);

// ICONDIR
bw.Write((ushort)0);           // reserved
bw.Write((ushort)1);           // type = icon
bw.Write((ushort)sizes.Length);

int dataOffset = 6 + 16 * sizes.Length;
for (int i = 0; i < sizes.Length; i++)
{
    byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
    bw.Write(dim);             // width  (0 = 256)
    bw.Write(dim);             // height (0 = 256)
    bw.Write((byte)0);         // color count
    bw.Write((byte)0);         // reserved
    bw.Write((ushort)1);       // planes
    bw.Write((ushort)32);      // bit count
    bw.Write((uint)frames[i].Length);
    bw.Write((uint)dataOffset);
    dataOffset += frames[i].Length;
}
foreach (var f in frames) bw.Write(f);

Console.WriteLine($"Written {icoPath} ({string.Join(", ", sizes)}px)");

static byte[] BuildDib(SKBitmap bmp)
{
    int w = bmp.Width, h = bmp.Height;
    // AND mask: 1 bit per pixel, row padded to DWORD
    int maskRowBytes = ((w + 31) / 32) * 4;
    int maskSize     = maskRowBytes * h;
    int pixelSize    = w * h * 4;     // 32bpp BGRA
    int dibSize      = 40 + pixelSize + maskSize;

    var buf = new byte[dibSize];
    var bw  = new BinaryWriter(new MemoryStream(buf));

    // BITMAPINFOHEADER (height doubled per ICO spec)
    bw.Write(40);          // biSize
    bw.Write(w);           // biWidth
    bw.Write(h * 2);       // biHeight (XOR + AND masks)
    bw.Write((ushort)1);   // biPlanes
    bw.Write((ushort)32);  // biBitCount
    bw.Write(0);           // biCompression = BI_RGB
    bw.Write(pixelSize);   // biSizeImage
    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

    // Pixel data bottom-up
    for (int row = h - 1; row >= 0; row--)
    {
        for (int col = 0; col < w; col++)
        {
            var px = bmp.GetPixel(col, row);
            bw.Write(px.Blue);
            bw.Write(px.Green);
            bw.Write(px.Red);
            bw.Write(px.Alpha);
        }
    }

    // AND mask (all 0 = fully opaque — alpha channel handles transparency)
    for (int i = 0; i < maskSize; i++) bw.Write((byte)0);

    return buf;
}
