// Smoke test: SkiaSharp 3.119.4's managed bindings with the library built here (copied over the package's own, which
// would fail it): a PDF's streams deflated at zlib's fastest level when the metadata names none (their zlib header
// 78 01), at the level it names otherwise (in the encoding quality: 10101 is level 9, 78 DA), and a raster drawing.
using System.Text;
using SkiaSharp;

bool ok = Deflated(101, 0x01) & Deflated(10101, 0xDA) & Deflated(7101, 0x9C);

// A page written with the encoding quality given: every deflated stream's second byte the one expected
static bool Deflated(int quality, byte expected)
{
    var stream = new SKDynamicMemoryWStream();
    using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { Title = "Smoke test", Producer = "Smoke test", EncodingQuality = quality }))
    {
        var canvas = document.BeginPage(300, 200);
        using var paint = new SKPaint { Color = SKColors.Navy, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 14);
        for (int line = 0; line < 8; line++) canvas.DrawText($"Line {line} of a page written by the library", 20, 30 + 18 * line, font, paint);
        canvas.DrawRect(200, 20, 60, 40, paint);
        canvas.DrawCircle(230, 120, 30, paint);
        document.EndPage();
        document.Close();
    }
    byte[] pdf;
    using (var data = stream.DetachAsData()) pdf = data.ToArray();
    int deflated = 0, right = 0;
    var text = Encoding.Latin1.GetString(pdf);
    for (int at = text.IndexOf("/FlateDecode", StringComparison.Ordinal); at >= 0; at = text.IndexOf("/FlateDecode", at + 1, StringComparison.Ordinal))
    {
        int body = text.IndexOf("stream\n", at, StringComparison.Ordinal);
        if (body < 0) break;
        body += "stream\n".Length;
        deflated++;
        if (pdf[body] == 0x78 && pdf[body + 1] == expected) right++;
        else Console.WriteLine($"quality {quality}: a stream's zlib header {pdf[body]:X2} {pdf[body + 1]:X2}, not 78 {expected:X2}");
    }
    Console.WriteLine($"quality {quality}: {pdf.Length} bytes, {deflated} streams deflated, {right} with 78 {expected:X2}");
    return deflated > 0 && right == deflated;
}

using var bitmap = new SKBitmap(64, 64);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.White);
    using var paint = new SKPaint { Color = SKColors.Navy };
    canvas.DrawCircle(32, 32, 20, paint);
}
var centre = bitmap.GetPixel(32, 32);
Console.WriteLine($"Raster: the centre pixel is {centre}");

ok &= centre == SKColors.Navy;
Console.WriteLine(ok ? "OK" : "FAILED");
return ok ? 0 : 1;
