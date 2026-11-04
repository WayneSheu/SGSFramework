using System.IO.Compression;
using System.Text;

namespace SGS.Modules.FMS.Application.Validation;

/// <summary>依檔案簽章判定的真實格式</summary>
/// <param name="Name">格式名稱（例如 JPEG、DOCX）</param>
/// <param name="Extension">儲存時使用的安全副檔名（小寫，含句點）</param>
/// <param name="ContentType">回應時使用的 MIME 類型</param>
/// <param name="IsImage">是否為可由影像處理器 (Magick.NET) 重新編碼 / 縮圖的點陣圖</param>
public sealed record DetectedFileFormat(string Name, string Extension, string ContentType, bool IsImage);

/// <summary>檔案簽章檢查結果</summary>
public sealed record FileInspectionResult(bool IsValid, DetectedFileFormat? Format, string? Error)
{
    public static FileInspectionResult Reject(string error) => new(false, null, error);
    public static FileInspectionResult Accept(DetectedFileFormat format) => new(true, format, null);
}

public interface IFileSignatureInspector
{
    /// <summary>
    /// 讀取檔頭魔術數字 (Magic Number) 判定真實格式，並與副檔名、白名單交叉比對。
    /// 串流須可定位 (CanSeek)，檢查後 Position 復原為 0。
    /// </summary>
    Task<FileInspectionResult> InspectAsync(Stream content, string fileName, IReadOnlyCollection<string> allowedExtensions, CancellationToken cancellationToken = default);
}

/// <summary>
/// 輕量檔案簽章檢查（不引入第三方套件）：嚴禁只信任檔名副檔名或 HTTP Content-Type。
/// <list type="bullet">
/// <item>點陣圖 / PDF：比對檔頭簽章。</item>
/// <item>OOXML（docx / xlsx / pptx）：ZIP 簽章 + 內容結構（[Content_Types].xml 與 word/、xl/、ppt/ 目錄），並拒絕含巨集（vbaProject.bin）的檔案。</item>
/// <item>舊版 Office（doc / xls / ppt）：OLE2 複合文件簽章（無法再細分，副檔名限三者之一）。</item>
/// <item>純文字（txt / csv）：無簽章，改驗證為合法 UTF-8 且不含 NUL 位元組，並拒絕 HTML / Script 開頭。</item>
/// <item>可執行檔（MZ / ELF / Mach-O / Shebang）一律拒絕，不論副檔名為何。</item>
/// </list>
/// </summary>
public sealed class FileSignatureInspector : IFileSignatureInspector
{
    private const int HeaderLength = 32;
    private const int TextSampleLength = 8192;
    private const int MaxZipEntriesToScan = 5000;

    private static readonly DetectedFileFormat Jpeg = new("JPEG", ".jpg", "image/jpeg", true);
    private static readonly DetectedFileFormat Png = new("PNG", ".png", "image/png", true);
    private static readonly DetectedFileFormat Gif = new("GIF", ".gif", "image/gif", true);
    private static readonly DetectedFileFormat Bmp = new("BMP", ".bmp", "image/bmp", true);
    private static readonly DetectedFileFormat Webp = new("WEBP", ".webp", "image/webp", true);
    private static readonly DetectedFileFormat Tiff = new("TIFF", ".tif", "image/tiff", true);
    private static readonly DetectedFileFormat Pdf = new("PDF", ".pdf", "application/pdf", false);
    private static readonly DetectedFileFormat Docx = new("DOCX", ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", false);
    private static readonly DetectedFileFormat Xlsx = new("XLSX", ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", false);
    private static readonly DetectedFileFormat Pptx = new("PPTX", ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", false);

    /// <summary>各格式可接受的副檔名（例如 .jpeg 與 .jpg 皆為 JPEG）</summary>
    private static readonly Dictionary<string, string[]> AcceptedExtensions = new(StringComparer.Ordinal)
    {
        ["JPEG"] = [".jpg", ".jpeg"],
        ["TIFF"] = [".tif", ".tiff"]
    };

    private static readonly Dictionary<string, DetectedFileFormat> Ole2Formats = new(StringComparer.Ordinal)
    {
        [".doc"] = new("DOC", ".doc", "application/msword", false),
        [".xls"] = new("XLS", ".xls", "application/vnd.ms-excel", false),
        [".ppt"] = new("PPT", ".ppt", "application/vnd.ms-powerpoint", false)
    };

    private static readonly Dictionary<string, DetectedFileFormat> TextFormats = new(StringComparer.Ordinal)
    {
        [".txt"] = new("TXT", ".txt", "text/plain; charset=utf-8", false),
        [".csv"] = new("CSV", ".csv", "text/csv; charset=utf-8", false)
    };

    private static readonly string[] DangerousTextPrefixes = ["<!doctype html", "<html", "<script", "<?php", "<%", "<svg", "<?xml"];

    public async Task<FileInspectionResult> InspectAsync(Stream content, string fileName, IReadOnlyCollection<string> allowedExtensions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(allowedExtensions);
        if (!content.CanSeek) throw new ArgumentException("檔案簽章檢查需要可定位的串流。", nameof(content));

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return FileInspectionResult.Reject($"不允許的檔案類型「{extension}」。");
        }

        if (content.Length == 0)
        {
            return FileInspectionResult.Reject("檔案內容為空。");
        }

        try
        {
            content.Position = 0;
            var header = new byte[HeaderLength];
            var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            var span = header.AsSpan(0, read);

            if (IsExecutable(span))
            {
                return FileInspectionResult.Reject("資安警報：檔案內容為可執行檔，拒絕上傳。");
            }

            var format = DetectBinary(span);
            if (format is null && span.StartsWith("PK\x03\x04"u8))
            {
                content.Position = 0;
                var (zipFormat, zipError) = InspectZip(content);
                if (zipError is not null) return FileInspectionResult.Reject(zipError);
                format = zipFormat;
            }
            else if (format is null && span.StartsWith((ReadOnlySpan<byte>)[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]))
            {
                format = Ole2Formats.GetValueOrDefault(extension);
                if (format is null) return Mismatch();
            }
            else if (format is null && TextFormats.TryGetValue(extension, out var textFormat))
            {
                content.Position = 0;
                var textError = await InspectTextAsync(content, cancellationToken).ConfigureAwait(false);
                if (textError is not null) return FileInspectionResult.Reject(textError);
                format = textFormat;
            }

            if (format is null)
            {
                return FileInspectionResult.Reject("無法辨識檔案格式，或格式不在允許清單內。");
            }

            var accepted = AcceptedExtensions.GetValueOrDefault(format.Name) ?? [format.Extension];
            return accepted.Contains(extension, StringComparer.Ordinal)
                ? FileInspectionResult.Accept(format with { Extension = extension })
                : Mismatch();
        }
        finally
        {
            content.Position = 0;
        }

        static FileInspectionResult Mismatch() => FileInspectionResult.Reject("資安警報：檔案內容與副檔名不符！");
    }

    private static bool IsExecutable(ReadOnlySpan<byte> header) =>
        (header.StartsWith("MZ"u8) && header.Contains((byte)0)) ||          // Windows PE / DOS（排除 MZ 開頭的純文字）
        header.StartsWith((ReadOnlySpan<byte>)[0x7F, 0x45, 0x4C, 0x46]) ||   // Linux ELF
        header.StartsWith((ReadOnlySpan<byte>)[0xCF, 0xFA, 0xED, 0xFE]) ||   // Mach-O 64
        header.StartsWith((ReadOnlySpan<byte>)[0xCE, 0xFA, 0xED, 0xFE]) ||   // Mach-O 32
        header.StartsWith((ReadOnlySpan<byte>)[0xCA, 0xFE, 0xBA, 0xBE]) ||   // Mach-O Fat / Java class
        header.StartsWith("#!"u8);                                           // Shell Script

    private static DetectedFileFormat? DetectBinary(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return Jpeg;
        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return Png;
        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8)) return Gif;
        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8)) return Webp;
        if (header.StartsWith((ReadOnlySpan<byte>)[0x49, 0x49, 0x2A, 0x00]) || header.StartsWith((ReadOnlySpan<byte>)[0x4D, 0x4D, 0x00, 0x2A])) return Tiff;
        if (header.StartsWith("%PDF-"u8)) return Pdf;
        if (header.Length >= 14 && header.StartsWith("BM"u8)) return Bmp;
        return null;
    }

    /// <summary>OOXML 結構檢查：須有 [Content_Types].xml，依主要目錄判定 docx / xlsx / pptx，含巨集者拒絕</summary>
    private static (DetectedFileFormat? Format, string? Error) InspectZip(Stream content)
    {
        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            bool hasContentTypes = false, word = false, excel = false, powerPoint = false;
            var scanned = 0;

            foreach (var entry in archive.Entries)
            {
                if (++scanned > MaxZipEntriesToScan) return (null, "壓縮結構項目過多，拒絕上傳。");

                var name = entry.FullName;
                if (name.Contains("..", StringComparison.Ordinal)) return (null, "資安警報：檔案內含不合法的路徑。");
                if (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)) return (null, "資安警報：不允許含巨集 (VBA) 的 Office 文件。");

                if (name.Equals("[Content_Types].xml", StringComparison.Ordinal)) hasContentTypes = true;
                else if (name.StartsWith("word/", StringComparison.Ordinal)) word = true;
                else if (name.StartsWith("xl/", StringComparison.Ordinal)) excel = true;
                else if (name.StartsWith("ppt/", StringComparison.Ordinal)) powerPoint = true;
            }

            if (!hasContentTypes) return (null, "不允許一般壓縮檔，僅接受 Office Open XML 文件。");

            return (word, excel, powerPoint) switch
            {
                (true, false, false) => (Docx, null),
                (false, true, false) => (Xlsx, null),
                (false, false, true) => (Pptx, null),
                _ => (null, "無法辨識的 Office 文件結構。")
            };
        }
        catch (InvalidDataException)
        {
            return (null, "壓縮結構損毀，無法辨識檔案格式。");
        }
    }

    /// <summary>純文字：前 8KB 須為合法 UTF-8（可含 BOM）、不含 NUL，且不以 HTML / Script / XML 標記開頭</summary>
    private static async Task<string?> InspectTextAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new byte[TextSampleLength];
        var read = await content.ReadAtLeastAsync(buffer, TextSampleLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        var sample = buffer.AsSpan(0, read);

        if (sample.IndexOf((byte)0) >= 0) return "文字檔不得包含二進位內容。";

        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        var chars = new char[Encoding.UTF8.GetMaxCharCount(read)];
        int charCount;
        try
        {
            // flush: false — 取樣可能截斷在多位元組字元中間，不視為錯誤
            charCount = decoder.GetChars(sample, chars, flush: content.Length <= TextSampleLength);
        }
        catch (DecoderFallbackException)
        {
            return "文字檔須為 UTF-8 編碼。";
        }

        var text = new string(chars, 0, charCount).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return DangerousTextPrefixes.Any(p => text.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            ? "資安警報：文字檔內容為標記語言或指令碼，拒絕上傳。"
            : null;
    }
}
