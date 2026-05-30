using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using BimExplorer.App.Services;

namespace BimExplorer.App.Views;

public partial class Viewer3DControl : UserControl
{
    private Point _lastMousePos;
    private bool _isRotating;
    private bool _isPanning;
    private double _rotationX = 30;
    private double _rotationZ = 45;
    private double _distance = 100;
    private Point3D _center;
    private readonly ModelVisual3D _modelVisual = new();

    public Viewer3DControl()
    {
        InitializeComponent();

        Viewport3D.Children.Add(_modelVisual);
        Viewport3D.MouseLeftButtonDown += OnMouseLeftButtonDown;
        Viewport3D.MouseLeftButtonUp += OnMouseLeftButtonUp;
        Viewport3D.MouseRightButtonDown += OnMouseRightButtonDown;
        Viewport3D.MouseRightButtonUp += OnMouseRightButtonUp;
        Viewport3D.MouseMove += OnMouseMove;
        Viewport3D.MouseWheel += OnMouseWheel;
    }

    public void ShowPlaceholder()
    {
        PlaceholderPanel.Visibility = Visibility.Visible;
        ThumbnailPanel.Visibility = Visibility.Collapsed;
        ViewportPanel.Visibility = Visibility.Collapsed;
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    public void ShowThumbnail(BitmapImage? thumbnail, string extension, string fileName, string? error = null)
    {
        PlaceholderPanel.Visibility = Visibility.Collapsed;
        ViewportPanel.Visibility = Visibility.Collapsed;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ThumbnailPanel.Visibility = Visibility.Visible;

        ThumbnailImage.Source = thumbnail;
        ThumbnailImage.Visibility = thumbnail != null ? Visibility.Visible : Visibility.Collapsed;
        FileTypeLabel.Text = extension;
        FileNameLabel.Text = fileName;

        if (!string.IsNullOrEmpty(error))
        {
            ErrorLabel.Text = error;
            ErrorLabel.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorLabel.Visibility = Visibility.Collapsed;
        }
    }

    public async Task LoadIfcModelAsync(string ifcPath)
    {
        PlaceholderPanel.Visibility = Visibility.Collapsed;
        ThumbnailPanel.Visibility = Visibility.Collapsed;
        ViewportPanel.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            var result = await Task.Run(() => IfcGeometryReader.BuildMesh(ifcPath));

            if (result.mesh == null)
            {
                ShowThumbnail(null, ".IFC", Path.GetFileName(ifcPath), result.error);
                return;
            }

            var mesh = result.mesh;
            var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(140, 180, 210)));
            var backMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(100, 140, 170)));

            var group = new Model3DGroup();
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = backMaterial });
            group.Children.Add(new AmbientLight(Color.FromRgb(80, 80, 80)));
            group.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1)));
            group.Children.Add(new DirectionalLight(Color.FromRgb(60, 60, 60), new Vector3D(1, 0.5, 0.5)));

            _modelVisual.Content = group;
            var bounds = result.bounds;
            _center = new Point3D(
                bounds.X + bounds.SizeX / 2,
                bounds.Y + bounds.SizeY / 2,
                bounds.Z + bounds.SizeZ / 2);
            _distance = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ)) * 2;
            if (_distance < 0.01) _distance = 10;
            _rotationX = 30;
            _rotationZ = 45;
            UpdateCamera();
        }
        catch (Exception ex)
        {
            ShowThumbnail(null, ".IFC", Path.GetFileName(ifcPath), ex.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCamera()
    {
        var radX = _rotationX * Math.PI / 180;
        var radZ = _rotationZ * Math.PI / 180;

        var x = _distance * Math.Cos(radX) * Math.Cos(radZ);
        var y = _distance * Math.Cos(radX) * Math.Sin(radZ);
        var z = _distance * Math.Sin(radX);

        Camera.Position = new Point3D(_center.X + x, _center.Y + y, _center.Z + z);
        Camera.LookDirection = new Vector3D(-x, -y, -z);
        Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRotating = true;
        _lastMousePos = e.GetPosition(this);
        Viewport3D.CaptureMouse();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRotating = false;
        Viewport3D.ReleaseMouseCapture();
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _lastMousePos = e.GetPosition(this);
        Viewport3D.CaptureMouse();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        Viewport3D.ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var dx = pos.X - _lastMousePos.X;
        var dy = pos.Y - _lastMousePos.Y;
        _lastMousePos = pos;

        if (_isRotating)
        {
            _rotationZ += dx * 0.5;
            _rotationX = Math.Clamp(_rotationX + dy * 0.5, -89, 89);
            UpdateCamera();
        }
        else if (_isPanning)
        {
            var panScale = _distance * 0.002;
            var radZ = _rotationZ * Math.PI / 180;
            _center.X += (-dx * Math.Sin(radZ) - dy * Math.Cos(radZ) * Math.Sin(_rotationX * Math.PI / 180)) * panScale;
            _center.Y += (dx * Math.Cos(radZ) - dy * Math.Sin(radZ) * Math.Sin(_rotationX * Math.PI / 180)) * panScale;
            _center.Z += dy * Math.Cos(_rotationX * Math.PI / 180) * panScale;
            UpdateCamera();
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _distance *= e.Delta > 0 ? 0.9 : 1.1;
        _distance = Math.Max(_distance, 0.1);
        UpdateCamera();
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        _rotationX = 30;
        _rotationZ = 45;
        if (_modelVisual.Content is Model3DGroup group)
        {
            var bounds = group.Bounds;
            _center = new Point3D(
                bounds.X + bounds.SizeX / 2,
                bounds.Y + bounds.SizeY / 2,
                bounds.Z + bounds.SizeZ / 2);
            _distance = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ)) * 2;
        }
        UpdateCamera();
    }

    private void TopView_Click(object sender, RoutedEventArgs e)
    {
        _rotationX = 89;
        _rotationZ = 0;
        UpdateCamera();
    }

    private void FrontView_Click(object sender, RoutedEventArgs e)
    {
        _rotationX = 0;
        _rotationZ = 0;
        UpdateCamera();
    }
}
