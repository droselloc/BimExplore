using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Assimp;
using WpfVector3D = System.Windows.Media.Media3D.Vector3D;

namespace BimExplorer.App.Services;

internal static class FbxThumbnailGenerator
{
    public static byte[]? GeneratePreview(string fbxPath, int size = 256)
    {
        if (!File.Exists(fbxPath))
            return null;

        try
        {
            byte[]? result = null;
            var thread = new Thread(() => result = RenderOnStaThread(fbxPath, size));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? RenderOnStaThread(string fbxPath, int size)
    {
        try
        {
            var mesh = LoadFbxMesh(fbxPath);
            if (mesh == null || mesh.Positions.Count == 0)
                return null;

            var bitmap = Render3DToBitmap(mesh, size, size);
            return EncodePng(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private static MeshGeometry3D? LoadFbxMesh(string fbxPath)
    {
        using var importer = new AssimpContext();
        var scene = importer.ImportFile(fbxPath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.GenerateNormals |
            PostProcessSteps.JoinIdenticalVertices);

        if (scene == null || !scene.HasMeshes)
            return null;

        var wpfMesh = new MeshGeometry3D();
        var vertexOffset = 0;

        foreach (var assimpMesh in scene.Meshes)
        {
            foreach (var vertex in assimpMesh.Vertices)
                wpfMesh.Positions.Add(new Point3D(vertex.X, vertex.Y, vertex.Z));

            if (assimpMesh.HasNormals)
            {
                foreach (var normal in assimpMesh.Normals)
                    wpfMesh.Normals.Add(new WpfVector3D(normal.X, normal.Y, normal.Z));
            }

            foreach (var face in assimpMesh.Faces)
            {
                if (face.IndexCount == 3)
                {
                    wpfMesh.TriangleIndices.Add(face.Indices[0] + vertexOffset);
                    wpfMesh.TriangleIndices.Add(face.Indices[1] + vertexOffset);
                    wpfMesh.TriangleIndices.Add(face.Indices[2] + vertexOffset);
                }
            }

            vertexOffset += assimpMesh.VertexCount;
        }

        return wpfMesh.Positions.Count > 0 ? wpfMesh : null;
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
        modelGroup.Children.Add(new DirectionalLight(Colors.White, new WpfVector3D(-1, -1, -1)));
        modelGroup.Children.Add(new DirectionalLight(Color.FromRgb(60, 60, 60), new WpfVector3D(1, 0.5, 0.5)));

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
            LookDirection = new WpfVector3D(-0.7 * distance, -0.5 * distance, -0.7 * distance),
            UpDirection = new WpfVector3D(0, 0, 1),
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

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
