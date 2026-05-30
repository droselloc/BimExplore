using System.IO;
using System.Security.Cryptography;
using BimExplorer.Data;
using BimExplorer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BimExplorer.App.Services;

public class FileIndexerService(BimDbContext db, ThumbnailService thumbnailService)
{
    private static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rfa"
    };

    private static readonly HashSet<string> FbxExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".fbx"
    };

    private static readonly HashSet<string> RvtExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rvt"
    };

    public async Task<int> IndexDirectoryAsync(string directoryPath, IProgress<(string message, int current, int total)>? progress = null, bool includeFbx = false, bool includeRvt = false)
    {
        var allowedExtensions = new HashSet<string>(DefaultExtensions, StringComparer.OrdinalIgnoreCase);
        if (includeFbx)
            foreach (var ext in FbxExtensions) allowedExtensions.Add(ext);
        if (includeRvt)
            foreach (var ext in RvtExtensions) allowedExtensions.Add(ext);

        var rootPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var existingFolder = await db.IndexedFolders.FirstOrDefaultAsync(f => f.Path == rootPath);
        if (existingFolder == null)
        {
            db.IndexedFolders.Add(new IndexedFolder { Path = rootPath, LastIndexedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        else
        {
            existingFolder.LastIndexedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => allowedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var count = 0;
        var total = files.Count;

        for (var i = 0; i < files.Count; i++)
        {
            var filePath = files[i];
            var normalizedPath = Path.GetFullPath(filePath);
            progress?.Report(($"Indexando ({i + 1}/{total}): {Path.GetFileName(filePath)}", i + 1, total));

            var exists = await db.BimFiles.AnyAsync(f => f.FilePath == normalizedPath);
            if (exists)
                continue;

            try
            {
                var fileInfo = new FileInfo(normalizedPath);
                var hash = await ComputeHashAsync(normalizedPath);

                var bimFile = new BimFile
                {
                    FileName = fileInfo.Name,
                    FilePath = normalizedPath,
                    FileSizeBytes = fileInfo.Length,
                    FileHash = hash,
                    CreatedAtUtc = fileInfo.CreationTimeUtc,
                    LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                    IndexedAtUtc = DateTime.UtcNow,
                    ProjectName = ExtractProjectName(normalizedPath),
                    Discipline = ExtractDiscipline(fileInfo.Name)
                };

                // Generate thumbnail
                progress?.Report(($"Generando preview: {fileInfo.Name}", i + 1, total));
                bimFile.ThumbnailPath = thumbnailService.GenerateThumbnail(normalizedPath, hash);

                db.BimFiles.Add(bimFile);
                await db.SaveChangesAsync();
                count++;
            }
            catch (DbUpdateException ex)
            {
                // Detach the failed entity so it doesn't block subsequent saves
                foreach (var entry in db.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
                {
                    entry.State = EntityState.Detached;
                }

                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                progress?.Report(($"Error indexando {Path.GetFileName(filePath)}: {innerMsg}", i + 1, total));
            }
        }

        return count;
    }

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hashBytes);
    }

    private static string? ExtractProjectName(string filePath)
    {
        // Use parent directory name as project name heuristic
        var dir = Path.GetDirectoryName(filePath);
        return dir != null ? new DirectoryInfo(dir).Name : null;
    }

    private static string? ExtractDiscipline(string fileName)
    {
        // Common BIM discipline prefixes
        var upper = fileName.ToUpperInvariant();
        if (upper.StartsWith("ARC") || upper.Contains("_ARC")) return "Architecture";
        if (upper.StartsWith("STR") || upper.Contains("_STR")) return "Structural";
        if (upper.StartsWith("MEP") || upper.Contains("_MEP")) return "MEP";
        if (upper.StartsWith("ELE") || upper.Contains("_ELE")) return "Electrical";
        if (upper.StartsWith("PLU") || upper.Contains("_PLU")) return "Plumbing";
        if (upper.StartsWith("HVA") || upper.Contains("_HVA")) return "HVAC";
        return null;
    }
}
