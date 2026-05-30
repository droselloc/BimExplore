using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace BimExplorer.App.Services;

/// <summary>
/// Reads IFC geometry directly from the data model using Xbim.Essentials,
/// without requiring the native Xbim.Geometry engine.
/// Supports: IfcTriangulatedFaceSet, IfcPolygonalFaceSet, IfcFacetedBrep, IfcPolyLoop.
/// </summary>
internal static class IfcGeometryReader
{
    public static (MeshGeometry3D? mesh, Rect3D bounds, string? error) BuildMesh(string ifcPath)
    {
        try
        {
            using var model = IfcStore.Open(ifcPath);
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            // Iterate all products and extract geometry
            var products = model.Instances.OfType<IIfcProduct>();
            foreach (var product in products)
            {
                var rep = product.Representation;
                if (rep == null) continue;

                foreach (var shapeRep in rep.Representations)
                {
                    // Prefer Body representation
                    if (shapeRep.RepresentationIdentifier?.ToString() is string id
                        && id != "Body" && id != "Facetation" && id != "SolidModel"
                        && shapeRep.RepresentationIdentifier.ToString() != "Body")
                    {
                        // Still process if no Body is available
                    }

                    foreach (var item in shapeRep.Items)
                    {
                        ProcessRepresentationItem(item, positions, normals, indices);
                    }
                }
            }

            if (positions.Count == 0)
                return (null, default, "No se encontro geometria renderizable en el archivo IFC");

            var mesh = new MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };

            return (mesh, mesh.Bounds, null);
        }
        catch (Exception ex)
        {
            return (null, default, $"Error al leer IFC: {ex.Message}");
        }
    }

    private static void ProcessRepresentationItem(
        IIfcRepresentationItem item,
        Point3DCollection positions,
        Vector3DCollection normals,
        Int32Collection indices)
    {
        switch (item)
        {
            case IIfcTriangulatedFaceSet tfs:
                ReadTriangulatedFaceSet(tfs, positions, normals, indices);
                break;

            case IIfcPolygonalFaceSet pfs:
                ReadPolygonalFaceSet(pfs, positions, normals, indices);
                break;

            case IIfcFacetedBrep brep:
                ReadFacetedBrep(brep, positions, normals, indices);
                break;

            case IIfcBooleanResult boolResult:
                // Try to extract geometry from operands
                if (boolResult.FirstOperand is IIfcRepresentationItem first)
                    ProcessRepresentationItem(first, positions, normals, indices);
                if (boolResult.SecondOperand is IIfcRepresentationItem second)
                    ProcessRepresentationItem(second, positions, normals, indices);
                break;

            case IIfcMappedItem mapped:
                if (mapped.MappingSource?.MappedRepresentation is { } mappedRep)
                {
                    foreach (var sub in mappedRep.Items)
                        ProcessRepresentationItem(sub, positions, normals, indices);
                }
                break;
        }
    }

    private static void ReadTriangulatedFaceSet(
        IIfcTriangulatedFaceSet tfs,
        Point3DCollection positions,
        Vector3DCollection normals,
        Int32Collection indices)
    {
        var coords = tfs.Coordinates.CoordList.ToList();

        foreach (var triangle in tfs.CoordIndex)
        {
            var triIndices = triangle.ToList();
            if (triIndices.Count < 3) continue;

            var baseIndex = positions.Count;

            for (var i = 0; i < 3; i++)
            {
                // IFC indices are 1-based
                var coordIdx = (int)triIndices[i].Value - 1;
                if (coordIdx < 0 || coordIdx >= coords.Count) continue;

                var coord = coords[coordIdx].ToList();
                if (coord.Count < 3) continue;

                positions.Add(new Point3D(
                    (double)coord[0].Value,
                    (double)coord[1].Value,
                    (double)coord[2].Value));

                indices.Add(baseIndex + i);
            }

            // Compute face normal from the three vertices
            AddFaceNormal(positions, normals, baseIndex);
        }
    }

    private static void ReadPolygonalFaceSet(
        IIfcPolygonalFaceSet pfs,
        Point3DCollection positions,
        Vector3DCollection normals,
        Int32Collection indices)
    {
        var coords = pfs.Coordinates.CoordList.ToList();

        foreach (var face in pfs.Faces)
        {
            var faceIndices = face.CoordIndex.Select(i => (int)i.Value - 1).ToList();
            if (faceIndices.Count < 3) continue;

            // Fan triangulation for polygons with more than 3 vertices
            var baseVerts = new List<Point3D>();
            foreach (var idx in faceIndices)
            {
                if (idx < 0 || idx >= coords.Count) continue;
                var coord = coords[idx].ToList();
                if (coord.Count < 3) continue;
                baseVerts.Add(new Point3D(
                    (double)coord[0].Value,
                    (double)coord[1].Value,
                    (double)coord[2].Value));
            }

            TriangulateFan(baseVerts, positions, normals, indices);
        }
    }

    private static void ReadFacetedBrep(
        IIfcFacetedBrep brep,
        Point3DCollection positions,
        Vector3DCollection normals,
        Int32Collection indices)
    {
        var outer = brep.Outer;
        if (outer == null) return;

        foreach (var face in outer.CfsFaces)
        {
            foreach (var bound in face.Bounds)
            {
                if (bound.Bound is not IIfcPolyLoop polyLoop) continue;

                var polygon = polyLoop.Polygon.ToList();
                if (polygon.Count < 3) continue;

                var verts = new List<Point3D>();
                foreach (var point in polygon)
                {
                    var coordValues = point.Coordinates.ToList();
                    if (coordValues.Count < 3) continue;
                    verts.Add(new Point3D(
                        (double)coordValues[0].Value,
                        (double)coordValues[1].Value,
                        (double)coordValues[2].Value));
                }

                // Reverse winding if orientation is false
                if (!bound.Orientation)
                    verts.Reverse();

                TriangulateFan(verts, positions, normals, indices);
            }
        }
    }

    /// <summary>
    /// Fan triangulation: vertex 0 connects to every consecutive edge.
    /// Works for convex polygons (which IFC faces typically are).
    /// </summary>
    private static void TriangulateFan(
        List<Point3D> verts,
        Point3DCollection positions,
        Vector3DCollection normals,
        Int32Collection indices)
    {
        if (verts.Count < 3) return;

        for (var i = 1; i + 1 < verts.Count; i++)
        {
            var baseIndex = positions.Count;
            positions.Add(verts[0]);
            positions.Add(verts[i]);
            positions.Add(verts[i + 1]);

            indices.Add(baseIndex);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);

            AddFaceNormal(positions, normals, baseIndex);
        }
    }

    private static void AddFaceNormal(Point3DCollection positions, Vector3DCollection normals, int baseIndex)
    {
        if (baseIndex + 2 >= positions.Count)
        {
            normals.Add(new Vector3D(0, 0, 1));
            normals.Add(new Vector3D(0, 0, 1));
            normals.Add(new Vector3D(0, 0, 1));
            return;
        }

        var p0 = positions[baseIndex];
        var p1 = positions[baseIndex + 1];
        var p2 = positions[baseIndex + 2];

        var v1 = new Vector3D(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
        var v2 = new Vector3D(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);
        var normal = Vector3D.CrossProduct(v1, v2);
        if (normal.Length > 0)
            normal.Normalize();
        else
            normal = new Vector3D(0, 0, 1);

        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
    }
}
