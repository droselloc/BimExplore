using System.IO;
using OpenMcdf;

namespace BimExplorer.App.Services;

/// <summary>
/// Extracts embedded preview images from Revit (.rvt/.rfa) files.
/// Revit files are OLE Compound Files containing a "RevitPreview4.0" stream
/// with an embedded PNG image.
/// </summary>
internal static class RevitThumbnailExtractor
{
    // PNG magic bytes
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static byte[]? ExtractPreviewImage(string revitFilePath)
    {
        try
        {
            using var rootStorage = RootStorage.OpenRead(revitFilePath);

            // Try known Revit preview stream names
            string[] streamNames = ["RevitPreview4.0", "RevitPreview3.0", "RevitPreview2.0"];

            foreach (var name in streamNames)
            {
                if (rootStorage.TryOpenStream(name, out var stream))
                {
                    using (stream)
                    {
                        var data = ReadAllBytes(stream);
                        var pngData = ExtractPngFromData(data);
                        if (pngData != null)
                            return pngData;
                    }
                }
            }

            // Fallback: search all streams for PNG data
            foreach (var entry in rootStorage.EnumerateEntries())
            {
                if (entry.Type != EntryType.Stream) continue;

                if (rootStorage.TryOpenStream(entry.Name, out var stream))
                {
                    using (stream)
                    {
                        if (stream.Length > 8 && stream.Length < 50_000_000)
                        {
                            var data = ReadAllBytes(stream);
                            var pngData = ExtractPngFromData(data);
                            if (pngData != null)
                                return pngData;
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ReadAllBytes(CfbStream stream)
    {
        var buffer = new byte[stream.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        return buffer;
    }

    private static byte[]? ExtractPngFromData(byte[] data)
    {
        // Search for PNG signature in the data
        var offset = FindSequence(data, PngSignature);
        if (offset < 0)
            return null;

        // Extract from PNG header to end of data (PNG has its own end marker IEND)
        var pngData = new byte[data.Length - offset];
        Array.Copy(data, offset, pngData, 0, pngData.Length);

        // Verify it's a valid PNG by checking for IEND chunk
        var iendMarker = new byte[] { 0x49, 0x45, 0x4E, 0x44 }; // "IEND"
        var iendPos = FindSequence(pngData, iendMarker);
        if (iendPos > 0)
        {
            // Trim to just before the CRC after IEND (4 bytes after IEND marker)
            var endPos = iendPos + 4 + 4; // IEND + CRC
            if (endPos <= pngData.Length)
            {
                var trimmed = new byte[endPos];
                Array.Copy(pngData, 0, trimmed, 0, endPos);
                return trimmed;
            }
        }

        return pngData;
    }

    private static int FindSequence(byte[] data, byte[] sequence)
    {
        for (var i = 0; i <= data.Length - sequence.Length; i++)
        {
            var found = true;
            for (var j = 0; j < sequence.Length; j++)
            {
                if (data[i + j] != sequence[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
}
