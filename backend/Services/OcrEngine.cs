using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Veriqo.Api.Services;

public interface IOcrEngine
{
    Task<OcrResult> ExtractAsync(string filePath, string contentType);
}

public class OcrResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
    public double Confidence { get; set; }   // mean per-word confidence, 0..100
    public int WordCount { get; set; }
}

/// <summary>
/// Real OCR via the Tesseract CLI using TSV output, which gives both the text and a
/// per-word confidence we aggregate. Images are read directly; PDFs are rasterised with
/// pdftoppm first when available. Fails gracefully so an upload is never blocked by a
/// missing binary — the analyzer treats empty text / low confidence as risk signals.
/// </summary>
public class TesseractOcrEngine : IOcrEngine
{
    private readonly string _tesseract;
    private readonly string _pdftoppm;
    private readonly ILogger<TesseractOcrEngine> _log;

    public TesseractOcrEngine(IConfiguration config, ILogger<TesseractOcrEngine> log)
    {
        _tesseract = config["Ocr:TesseractPath"] ?? "tesseract";
        _pdftoppm = config["Ocr:PdfToPpmPath"] ?? "pdftoppm";
        _log = log;
    }

    public async Task<OcrResult> ExtractAsync(string filePath, string contentType)
    {
        try
        {
            var target = filePath;
            var tempImage = string.Empty;

            if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                tempImage = await ConvertPdfFirstPageAsync(filePath);
                if (string.IsNullOrEmpty(tempImage))
                    return new OcrResult { Success = false, Engine = "tesseract" };
                target = tempImage;
            }

            var result = await RunTesseractTsvAsync(target);

            if (!string.IsNullOrEmpty(tempImage) && File.Exists(tempImage))
                File.Delete(tempImage);

            return result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "OCR extraction failed for {File}", filePath);
            return new OcrResult { Success = false, Engine = "tesseract" };
        }
    }

    private async Task<OcrResult> RunTesseractTsvAsync(string imagePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _tesseract,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add("stdout");
        psi.ArgumentList.Add("tsv");

        using var proc = Process.Start(psi);
        if (proc == null) return new OcrResult { Success = false, Engine = "tesseract" };
        var tsv = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return ParseTsv(tsv);
    }

    private static OcrResult ParseTsv(string tsv)
    {
        var sb = new StringBuilder();
        var confidences = new List<double>();
        int lastLineKey = -1;

        foreach (var line in tsv.Split('\n'))
        {
            var cols = line.Split('\t');
            if (cols.Length < 12) continue;
            if (!int.TryParse(cols[0], out var level) || level != 5) continue; // word rows only

            var word = cols[11].Trim();
            if (word.Length == 0) continue;

            if (!double.TryParse(cols[10], NumberStyles.Any, CultureInfo.InvariantCulture, out var conf))
                conf = -1;
            if (conf >= 0) confidences.Add(conf);

            // reconstruct line breaks from block/par/line numbers
            var lineKey = HashKey(cols[2], cols[3], cols[4]);
            if (lastLineKey != -1 && lineKey != lastLineKey) sb.Append('\n');
            else if (sb.Length > 0) sb.Append(' ');
            sb.Append(word);
            lastLineKey = lineKey;
        }

        var text = sb.ToString().Trim();
        return new OcrResult
        {
            Success = text.Length > 0,
            Text = text,
            Engine = "tesseract",
            Confidence = confidences.Count > 0 ? Math.Round(confidences.Average(), 1) : 0,
            WordCount = confidences.Count
        };
    }

    private static int HashKey(string block, string par, string ln) =>
        HashCode.Combine(block, par, ln);

    private async Task<string> ConvertPdfFirstPageAsync(string pdfPath)
    {
        try
        {
            var outPrefix = Path.Combine(Path.GetTempPath(), "veriqo_" + Guid.NewGuid().ToString("N"));
            var psi = new ProcessStartInfo
            {
                FileName = _pdftoppm,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-png");
            psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-l"); psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-r"); psi.ArgumentList.Add("200");
            psi.ArgumentList.Add(pdfPath);
            psi.ArgumentList.Add(outPrefix);

            using var proc = Process.Start(psi);
            if (proc == null) return string.Empty;
            await proc.WaitForExitAsync();

            var produced = Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(outPrefix) + "*");
            return produced.FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
