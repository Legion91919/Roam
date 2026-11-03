using CarRental.Domain.Common;

namespace CarRental.Application.Common.Files;

/// <summary>
/// The upload rules, in one place, BEFORE anything is stored:
///  * At most 10 MB (file.too_large); not empty (file.empty).
///  * Only JPEG, PNG and PDF, decided by the file's MAGIC BYTES, never by its extension or the
///    Content-Type the browser sent (file.type_not_allowed). A renamed .exe is refused.
///  * JPEG and PNG lose their metadata blocks (EXIF, XMP), which can hold the GPS position where
///    the photo was taken (privacy, POPIA). The picture itself is untouched.
///  * The display name is cleaned: no path, no control characters, at most 100 characters.
/// </summary>
public static class FileUploadPolicy
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public sealed record CheckedFile(byte[] Content, string ContentType, string FileName);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();

    /// <summary>Reads at most 10 MB + 1 byte, so an oversized upload is refused without buffering all of it.</summary>
    public static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
                throw new DomainException("file.too_large", "Files can be at most 10 MB.");
        }
        return buffer.ToArray();
    }

    public static CheckedFile Check(byte[] content, string? fileName)
    {
        if (content.Length == 0)
            throw new DomainException("file.empty", "The file is empty.");
        if (content.Length > MaxBytes)
            throw new DomainException("file.too_large", "Files can be at most 10 MB.");

        var span = content.AsSpan();
        var (contentType, extension, cleaned) =
            span.StartsWith(Jpeg) ? ("image/jpeg", ".jpg", StripJpegMetadata(content)) :
            span.StartsWith(Png) ? ("image/png", ".png", StripPngMetadata(content)) :
            span.StartsWith(Pdf) ? ("application/pdf", ".pdf", content) :
            throw new DomainException("file.type_not_allowed", "Only JPEG and PNG photos and PDF documents can be uploaded.");

        return new CheckedFile(cleaned, contentType, SafeName(fileName, extension));
    }

    public static string SafeName(string? fileName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension((fileName ?? string.Empty).Replace('\\', '/').Split('/')[^1]);
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.').ToArray()).Trim();
        if (name.Length == 0) name = "file";
        if (name.Length > 95) name = name[..95];
        return name + extension;
    }

    /// <summary>
    /// Drops APP1 (EXIF, XMP) and APP13 (IPTC) segments from a JPEG. Walks the marker segments up to
    /// the start of scan; anything malformed is refused rather than stored half-cleaned.
    /// </summary>
    private static byte[] StripJpegMetadata(byte[] jpeg)
    {
        using var output = new MemoryStream(jpeg.Length);
        output.Write(jpeg, 0, 2);   // SOI
        var i = 2;
        while (i + 4 <= jpeg.Length)
        {
            if (jpeg[i] != 0xFF)
                throw new DomainException("file.type_not_allowed", "The JPEG file is damaged.");
            var marker = jpeg[i + 1];
            if (marker == 0xDA)   // start of scan: copy the image data and the rest as-is
            {
                output.Write(jpeg, i, jpeg.Length - i);
                return output.ToArray();
            }
            var length = (jpeg[i + 2] << 8) | jpeg[i + 3];
            if (length < 2 || i + 2 + length > jpeg.Length)
                throw new DomainException("file.type_not_allowed", "The JPEG file is damaged.");
            if (marker is not (0xE1 or 0xED))
                output.Write(jpeg, i, 2 + length);
            i += 2 + length;
        }
        throw new DomainException("file.type_not_allowed", "The JPEG file is damaged.");
    }

    /// <summary>Drops eXIf, iTXt, tEXt and zTXt chunks (EXIF and text metadata) from a PNG.</summary>
    private static byte[] StripPngMetadata(byte[] png)
    {
        using var output = new MemoryStream(png.Length);
        output.Write(png, 0, 8);
        var i = 8;
        while (i + 12 <= png.Length)
        {
            var length = (png[i] << 24) | (png[i + 1] << 16) | (png[i + 2] << 8) | png[i + 3];
            if (length < 0 || i + 12 + length > png.Length)
                throw new DomainException("file.type_not_allowed", "The PNG file is damaged.");
            var type = System.Text.Encoding.ASCII.GetString(png, i + 4, 4);
            if (type is not ("eXIf" or "iTXt" or "tEXt" or "zTXt"))
                output.Write(png, i, 12 + length);
            i += 12 + length;
            if (type == "IEND") break;
        }
        return output.ToArray();
    }
}
