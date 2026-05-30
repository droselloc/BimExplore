using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace BimExplorer.App.Services;

public class ThumbnailService
{
    private readonly string _cacheDir;

    public ThumbnailService()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BimExplorer", "thumbnails");
        Directory.CreateDirectory(_cacheDir);
    }

    private static readonly HashSet<string> RevitExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rvt", ".rfa", ".rte", ".rft"
    };

    public string? GenerateThumbnail(string filePath, string? fileHash)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var cacheKey = fileHash ?? Path.GetFileNameWithoutExtension(filePath);
        var thumbPath = Path.Combine(_cacheDir, $"{cacheKey}.png");

        if (File.Exists(thumbPath))
            return thumbPath;

        try
        {
            if (ext == ".ifc")
                return GenerateIfcThumbnail(filePath, thumbPath);

            if (RevitExtensions.Contains(ext))
                return ExtractRevitThumbnail(filePath, thumbPath);

            if (ext is ".dxf")
                return GenerateDxfThumbnail(filePath, thumbPath);

            if (ext is ".fbx")
                return GenerateFbxThumbnail(filePath, thumbPath);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractRevitThumbnail(string revitPath, string outputPath)
    {
        var pngData = RevitThumbnailExtractor.ExtractPreviewImage(revitPath);
        if (pngData == null || pngData.Length < 8)
            return null;

        File.WriteAllBytes(outputPath, pngData);
        return outputPath;
    }

    private static string? GenerateDxfThumbnail(string dxfPath, string outputPath)
    {
        var pngData = DxfThumbnailGenerator.GeneratePreview(dxfPath);
        if (pngData == null || pngData.Length < 8)
            return null;

        File.WriteAllBytes(outputPath, pngData);
        return outputPath;
    }

    private static string? GenerateFbxThumbnail(string fbxPath, string outputPath)
    {
        var pngData = FbxThumbnailGenerator.GeneratePreview(fbxPath);
        if (pngData == null || pngData.Length < 8)
            return null;

        File.WriteAllBytes(outputPath, pngData);
        return outputPath;
    }

    private static string? GenerateIfcThumbnail(string ifcPath, string outputPath)
    {
        var (mesh, _, _) = IfcGeometryReader.BuildMesh(ifcPath);
        if (mesh == null)
            return null;

        var bitmap = Render3DToBitmap(mesh, 256, 256);
        SaveBitmap(bitmap, outputPath);
        return outputPath;
    }

    private static RenderTargetBitmap Render3DToBitmap(MeshGeometry3D mesh, int width, int height)
    {
        var viewport = new Viewport3D();

        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(120, 160, 200)));
        var backMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(80, 120, 160)));
        var geometryModel = new GeometryModel3D(mesh, material) { BackMaterial = backMaterial };

        var modelGroup = new Model3DGroup();
        modelGroup.Children.Add(geometryModel);
        modelGroup.Children.Add(new AmbientLight(Color.FromRgb(80, 80, 80)));
        modelGroup.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1)));
        modelGroup.Children.Add(new DirectionalLight(Color.FromRgb(60, 60, 60), new Vector3D(1, 0.5, 0.5)));

        var modelVisual = new ModelVisual3D { Content = modelGroup };
        viewport.Children.Add(modelVisual);

        var bounds = mesh.Bounds;
        var center = new Point3D(
            bounds.X + bounds.SizeX / 2,
            bounds.Y + bounds.SizeY / 2,
            bounds.Z + bounds.SizeZ / 2);
        var maxDim = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        var distance = maxDim * 2;

        var camera = new PerspectiveCamera
        {
            Position = new Point3D(center.X + distance * 0.7, center.Y + distance * 0.5, center.Z + distance * 0.7),
            LookDirection = new Vector3D(-0.7 * distance, -0.5 * distance, -0.7 * distance),
            UpDirection = new Vector3D(0, 0, 1),
            FieldOfView = 45
        };
        viewport.Camera = camera;

        viewport.Width = width;
        viewport.Height = height;
        viewport.Measure(new Size(width, height));
        viewport.Arrange(new Rect(0, 0, width, height));
        viewport.UpdateLayout();

        var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderBitmap.Render(viewport);
        return renderBitmap;
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    public static BitmapImage? LoadThumbnail(string? thumbnailPath)
    {
        if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath))
            return null;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(thumbnailPath, UriKind.Absolute);
        bitmap.DecodePixelWidth = 64;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
