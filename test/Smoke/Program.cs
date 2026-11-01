// Smoke test: SkiaSharp 3.119.4's managed bindings with the library built here (copied over the package's own, which
// would fail it): every stream of a PDF deflated at zlib's fastest level (its header 78 01), and a raster drawing.
using System.Text;
using SkiaSharp;

var stream = new SKDynamicMemoryWStream();
using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { Title = "Smoke test", Producer = "Smoke test" }))
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

int deflated = 0, fastest = 0;
var text = Encoding.Latin1.GetString(pdf);
for (int at = text.IndexOf("/FlateDecode", StringComparison.Ordinal); at >= 0; at = text.IndexOf("/FlateDecode", at + 1, StringComparison.Ordinal))
{
    int body = text.IndexOf("stream\n", at, StringComparison.Ordinal);
    if (body < 0) break;
    body += "stream\n".Length;
    deflated++;
    if (pdf[body] == 0x78 && pdf[body + 1] == 0x01) fastest++;
    else Console.WriteLine($"a stream deflated otherwise: header {pdf[body]:X2} {pdf[body + 1]:X2}");
}
Console.WriteLine($"PDF: {pdf.Length} bytes, {deflated} streams deflated, {fastest} at the fastest level");

using var bitmap = new SKBitmap(64, 64);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(SKColors.White);
    using var paint = new SKPaint { Color = SKColors.Navy };
    canvas.DrawCircle(32, 32, 20, paint);
}
var centre = bitmap.GetPixel(32, 32);
Console.WriteLine($"Raster: the centre pixel is {centre}");

bool ok = deflated > 0 && fastest == deflated && centre == SKColors.Navy;
Console.WriteLine(ok ? "OK" : "FAILED");
return ok ? 0 : 1;
