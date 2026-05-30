using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BimExplorer.App.Services;

/// <summary>
/// Parses DXF files and renders a 2D preview image from LINE, LWPOLYLINE,
/// POLYLINE, CIRCLE, and ARC entities.
/// </summary>
internal static class DxfThumbnailGenerator
{
    public static byte[]? GeneratePreview(string dxfPath, int size = 256)
    {
        try
        {
            var entities = ParseEntities(dxfPath);
            if (entities.Count == 0) return null;
            return RenderEntities(entities, size);
        }
        catch
        {
            return null;
        }
    }

    private static List<DxfEntity> ParseEntities(string path)
    {
        var entities = new List<DxfEntity>();
        var lines = File.ReadLines(path).GetEnumerator();
        var inEntities = false;

        while (lines.MoveNext())
        {
            var code = lines.Current.Trim();
            if (!lines.MoveNext()) break;
            var value = lines.Current.Trim();

            if (code == "2" && value == "ENTITIES") { inEntities = true; continue; }
            if (code == "0" && value == "ENDSEC" && inEntities) break;
            if (!inEntities) continue;

            if (code == "0")
            {
                switch (value)
                {
                    case "LINE":
                        var line = ParseLine(lines);
                        if (line != null) entities.Add(line);
                        break;
                    case "LWPOLYLINE":
                        var lw = ParseLwPolyline(lines);
                        if (lw != null) entities.Add(lw);
                        break;
                    case "CIRCLE":
                        var circle = ParseCircle(lines);
                        if (circle != null) entities.Add(circle);
                        break;
                    case "ARC":
                        var arc = ParseArc(lines);
                        if (arc != null) entities.Add(arc);
                        break;
                }
            }
        }
        return entities;
    }

    private static DxfLine? ParseLine(IEnumerator<string> lines)
    {
        double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
        while (lines.MoveNext())
        {
            var code = lines.Current.Trim();
            if (!lines.MoveNext()) break;
            var val = lines.Current.Trim();
            if (code == "0") break; // next entity
            switch (code)
            {
                case "10": x1 = D(val); break;
                case "20": y1 = D(val); break;
                case "11": x2 = D(val); break;
                case "21": y2 = D(val); break;
            }
        }
        return new DxfLine(x1, y1, x2, y2);
    }

    private static DxfPolyline? ParseLwPolyline(IEnumerator<string> lines)
    {
        var pts = new List<Point>();
        double cx = 0;
        var closed = false;
        var hasX = false;

        while (lines.MoveNext())
        {
            var code = lines.Current.Trim();
            if (!lines.MoveNext()) break;
            var val = lines.Current.Trim();
            if (code == "0") break;
            switch (code)
            {
                case "10":
                    if (hasX) pts.Add(new Point(cx, 0));
                    cx = D(val);
                    hasX = true;
                    break;
                case "20":
                    if (hasX) { pts.Add(new Point(cx, D(val))); hasX = false; }
                    break;
                case "70":
                    closed = (int.Parse(val) & 1) == 1;
                    break;
            }
        }
        return pts.Count >= 2 ? new DxfPolyline(pts, closed) : null;
    }

    private static DxfCircle? ParseCircle(IEnumerator<string> lines)
    {
        double cx = 0, cy = 0, r = 0;
        while (lines.MoveNext())
        {
            var code = lines.Current.Trim();
            if (!lines.MoveNext()) break;
            var val = lines.Current.Trim();
            if (code == "0") break;
            switch (code)
            {
                case "10": cx = D(val); break;
                case "20": cy = D(val); break;
                case "40": r = D(val); break;
            }
        }
        return r > 0 ? new DxfCircle(cx, cy, r) : null;
    }

    private static DxfArc? ParseArc(IEnumerator<string> lines)
    {
        double cx = 0, cy = 0, r = 0, startAngle = 0, endAngle = 360;
        while (lines.MoveNext())
        {
            var code = lines.Current.Trim();
            if (!lines.MoveNext()) break;
            var val = lines.Current.Trim();
            if (code == "0") break;
            switch (code)
            {
                case "10": cx = D(val); break;
                case "20": cy = D(val); break;
                case "40": r = D(val); break;
                case "50": startAngle = D(val); break;
                case "51": endAngle = D(val); break;
            }
        }
        return r > 0 ? new DxfArc(cx, cy, r, startAngle, endAngle) : null;
    }

    private static double D(string val) =>
        double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static byte[] RenderEntities(List<DxfEntity> entities, int size)
    {
        // Calculate bounds
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var e in entities)
        {
            var (eMinX, eMinY, eMaxX, eMaxY) = e.GetBounds();
            minX = Math.Min(minX, eMinX); minY = Math.Min(minY, eMinY);
            maxX = Math.Max(maxX, eMaxX); maxY = Math.Max(maxY, eMaxY);
        }

        var width = maxX - minX;
        var height = maxY - minY;
        if (width < 0.001) width = 1;
        if (height < 0.001) height = 1;

        var margin = size * 0.08;
        var drawSize = size - 2 * margin;
        var scale = Math.Min(drawSize / width, drawSize / height);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // Dark background
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
                null, new Rect(0, 0, size, size));

            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)), 1.2);
            pen.Freeze();

            foreach (var e in entities)
                e.Draw(dc, pen, minX, minY, scale, margin, size);
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    // Transform DXF coords to pixel coords (Y is flipped)
    private static Point Tx(double x, double y, double minX, double minY,
        double scale, double margin, int size)
        => new((x - minX) * scale + margin, size - ((y - minY) * scale + margin));

    private abstract record DxfEntity
    {
        public abstract (double minX, double minY, double maxX, double maxY) GetBounds();
        public abstract void Draw(DrawingContext dc, Pen pen, double minX, double minY,
            double scale, double margin, int size);
    }

    private record DxfLine(double X1, double Y1, double X2, double Y2) : DxfEntity
    {
        public override (double, double, double, double) GetBounds()
            => (Math.Min(X1, X2), Math.Min(Y1, Y2), Math.Max(X1, X2), Math.Max(Y1, Y2));

        public override void Draw(DrawingContext dc, Pen pen, double minX, double minY,
            double scale, double margin, int size)
        {
            dc.DrawLine(pen, Tx(X1, Y1, minX, minY, scale, margin, size),
                Tx(X2, Y2, minX, minY, scale, margin, size));
        }
    }

    private record DxfPolyline(List<Point> Points, bool Closed) : DxfEntity
    {
        public override (double, double, double, double) GetBounds()
        {
            double mnX = double.MaxValue, mnY = double.MaxValue;
            double mxX = double.MinValue, mxY = double.MinValue;
            foreach (var p in Points)
            {
                mnX = Math.Min(mnX, p.X); mnY = Math.Min(mnY, p.Y);
                mxX = Math.Max(mxX, p.X); mxY = Math.Max(mxY, p.Y);
            }
            return (mnX, mnY, mxX, mxY);
        }

        public override void Draw(DrawingContext dc, Pen pen, double minX, double minY,
            double scale, double margin, int size)
        {
            for (var i = 0; i + 1 < Points.Count; i++)
                dc.DrawLine(pen, Tx(Points[i].X, Points[i].Y, minX, minY, scale, margin, size),
                    Tx(Points[i + 1].X, Points[i + 1].Y, minX, minY, scale, margin, size));
            if (Closed && Points.Count > 2)
                dc.DrawLine(pen, Tx(Points[^1].X, Points[^1].Y, minX, minY, scale, margin, size),
                    Tx(Points[0].X, Points[0].Y, minX, minY, scale, margin, size));
        }
    }

    private record DxfCircle(double Cx, double Cy, double R) : DxfEntity
    {
        public override (double, double, double, double) GetBounds()
            => (Cx - R, Cy - R, Cx + R, Cy + R);

        public override void Draw(DrawingContext dc, Pen pen, double minX, double minY,
            double scale, double margin, int size)
        {
            var center = Tx(Cx, Cy, minX, minY, scale, margin, size);
            dc.DrawEllipse(null, pen, center, R * scale, R * scale);
        }
    }

    private record DxfArc(double Cx, double Cy, double R, double StartAngle, double EndAngle) : DxfEntity
    {
        public override (double, double, double, double) GetBounds()
            => (Cx - R, Cy - R, Cx + R, Cy + R);

        public override void Draw(DrawingContext dc, Pen pen, double minX, double minY,
            double scale, double margin, int size)
        {
            var sa = StartAngle * Math.PI / 180;
            var ea = EndAngle * Math.PI / 180;
            if (ea < sa) ea += 2 * Math.PI;
            var steps = Math.Max(16, (int)((ea - sa) / (Math.PI / 32)));
            var step = (ea - sa) / steps;

            for (var i = 0; i < steps; i++)
            {
                var a1 = sa + i * step;
                var a2 = sa + (i + 1) * step;
                var p1 = Tx(Cx + R * Math.Cos(a1), Cy + R * Math.Sin(a1), minX, minY, scale, margin, size);
                var p2 = Tx(Cx + R * Math.Cos(a2), Cy + R * Math.Sin(a2), minX, minY, scale, margin, size);
                dc.DrawLine(pen, p1, p2);
            }
        }
    }
}
