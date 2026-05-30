using System.IO;
using System.Windows.Media.Imaging;
using BimExplorer.App.Services;
using BimExplorer.Data.Models;

namespace BimExplorer.App.ViewModels;

public class BimFileViewModel
{
    private readonly BimFile _model;
    private readonly string _searchBlob;
    private BitmapImage? _thumbnail;
    private BitmapImage? _galleryThumbnail;
    private bool _thumbnailLoaded;
    private bool _galleryThumbnailLoaded;

    public BimFileViewModel(BimFile model)
    {
        _model = model;
        _searchBlob = BuildSearchBlob(model);
    }

    public int Id => _model.Id;
    public string FileName => _model.FileName;
    public string FilePath => _model.FilePath;
    public long FileSizeBytes => _model.FileSizeBytes;
    public string? FileHash => _model.FileHash;
    public DateTime? CreatedAtUtc => _model.CreatedAtUtc;
    public DateTime? LastModifiedUtc => _model.LastModifiedUtc;
    public DateTime IndexedAtUtc => _model.IndexedAtUtc;
    public string? ProjectName => _model.ProjectName;
    public string? Discipline => _model.Discipline;
    public string? AuthorName => _model.AuthorName;
    public string? SoftwareVersion => _model.SoftwareVersion;
    public string? ThumbnailPath => _model.ThumbnailPath;
    public string Extension => Path.GetExtension(_model.FileName).ToUpperInvariant();
    public string Tags => string.Join(", ", _model.BimFileTags.Select(t => t.Tag.Name));
    public bool IsIfc => Extension is ".IFC";
    public bool IsRevit => Extension is ".RVT" or ".RFA" or ".RTE" or ".RFT";
    public bool IsDxf => Extension is ".DXF";
    public bool IsRfa => Extension is ".RFA";
    public bool IsFbx => Extension is ".FBX";
    public bool IsRvt => Extension is ".RVT";
    public string Category => ExtractCategory(_model.FilePath);

    /// <summary>
    /// Fast substring match against a precomputed lowercase blob of
    /// filename, project, discipline, author and all tag names.
    /// </summary>
    public bool MatchesSearch(string queryLower) =>
        _searchBlob.Contains(queryLower);

    private static string BuildSearchBlob(BimFile model)
    {
        var tags = string.Join(' ', model.BimFileTags.Select(t => t.Tag.Name));
        return string.Concat(
            model.FileName, ' ',
            model.ProjectName ?? string.Empty, ' ',
            model.Discipline ?? string.Empty, ' ',
            model.AuthorName ?? string.Empty, ' ',
            tags).ToLowerInvariant();
    }

    public BitmapImage? Thumbnail
    {
        get
        {
            if (!_thumbnailLoaded)
            {
                _thumbnail = ThumbnailService.LoadThumbnail(_model.ThumbnailPath);
                _thumbnailLoaded = true;
            }
            return _thumbnail;
        }
    }

    /// <summary>
    /// Larger thumbnail for the gallery grid. Loads from cached file or
    /// extracts directly from Revit files on demand.
    /// </summary>
    public BitmapImage? GalleryThumbnail
    {
        get
        {
            if (!_galleryThumbnailLoaded)
            {
                _galleryThumbnail = LoadGalleryImage();
                _galleryThumbnailLoaded = true;
            }
            return _galleryThumbnail;
        }
    }

    public BitmapImage? PreviewThumbnail
    {
        get
        {
            if (!string.IsNullOrEmpty(_model.ThumbnailPath) && File.Exists(_model.ThumbnailPath))
                return LoadBitmap(_model.ThumbnailPath, 400);
            return GalleryThumbnail;
        }
    }

    private BitmapImage? LoadGalleryImage()
    {
        // Try cached thumbnail first
        if (!string.IsNullOrEmpty(_model.ThumbnailPath) && File.Exists(_model.ThumbnailPath))
            return LoadBitmap(_model.ThumbnailPath, 180);

        // For Revit files, try live extraction
        if (IsRevit)
        {
            var pngData = RevitThumbnailExtractor.ExtractPreviewImage(_model.FilePath);
            if (pngData is { Length: > 8 })
                return LoadBitmapFromBytes(pngData, 180);
        }

        // For DXF files, generate 2D preview on demand
        if (IsDxf)
        {
            var pngData = DxfThumbnailGenerator.GeneratePreview(_model.FilePath);
            if (pngData is { Length: > 8 })
                return LoadBitmapFromBytes(pngData, 180);
        }

        // FBX thumbnails are generated at indexing time only (too slow for on-demand)

        return null;
    }

    private static string ExtractCategory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        return dir != null ? new DirectoryInfo(dir).Name : "Sin categoria";
    }

    private static BitmapImage? LoadBitmap(string path, int decodeWidth)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private static BitmapImage? LoadBitmapFromBytes(byte[] data, int decodeWidth)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(data);
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }
}
