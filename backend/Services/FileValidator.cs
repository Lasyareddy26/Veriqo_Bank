namespace Veriqo.Api.Services;

public static class FileValidator
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>Detects the real file type from magic bytes, ignoring the extension.</summary>
    public static string? DetectType(byte[] bytes)
    {
        if (bytes.Length < 4) return null;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        // PDF: 25 50 44 46  (%PDF)
        if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) return "application/pdf";

        return null;
    }

    public static string Validate(byte[] bytes, out string detectedType)
    {
        detectedType = DetectType(bytes) ?? string.Empty;
        if (string.IsNullOrEmpty(detectedType))
            return "Unsupported file. Only JPG, PNG or PDF are allowed.";
        if (bytes.Length > MaxBytes)
            return "File is larger than the 5 MB limit.";
        return string.Empty;
    }
}
