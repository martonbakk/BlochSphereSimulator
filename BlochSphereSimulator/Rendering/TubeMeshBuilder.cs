using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BlochSphereSimulator.Rendering
{
    /// <summary>
    /// Builds thin open cylinders into one WPF mesh. Used for the state paths and the rotation axis:
    /// unlike screen-space lines, this is ordinary 3D geometry that is computed once and does not
    /// need to be rebuilt by the renderer on every frame.
    /// </summary>
    public sealed class TubeMeshBuilder
    {
        private readonly Point3DCollection _positions = new();
        private readonly Vector3DCollection _normals = new();
        private readonly Int32Collection _indices = new();

        public bool IsEmpty => _positions.Count == 0;

        public void AddCylinder(Point3D p1, Point3D p2, double diameter, int sides = 8)
        {
            var axis = p2 - p1;
            if (axis.Length < 1e-9) return;
            axis.Normalize();

            // Two unit vectors perpendicular to the axis
            var helper = Math.Abs(axis.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0);
            var u = Vector3D.CrossProduct(axis, helper);
            u.Normalize();
            var v = Vector3D.CrossProduct(axis, u);
            double r = diameter / 2;

            int start = _positions.Count;
            for (int i = 0; i < sides; i++)
            {
                double a = 2 * Math.PI * i / sides;
                var n = u * Math.Cos(a) + v * Math.Sin(a);
                _positions.Add(p1 + n * r);
                _positions.Add(p2 + n * r);
                _normals.Add(n);
                _normals.Add(n);
            }
            for (int i = 0; i < sides; i++)
            {
                int a0 = start + 2 * i, a1 = a0 + 1;
                int b0 = start + 2 * ((i + 1) % sides), b1 = b0 + 1;
                _indices.Add(a0); _indices.Add(b0); _indices.Add(a1);
                _indices.Add(a1); _indices.Add(b0); _indices.Add(b1);
            }
        }

        public GeometryModel3D? ToModel(Color color)
        {
            if (IsEmpty) return null;
            var mesh = new MeshGeometry3D { Positions = _positions, Normals = _normals, TriangleIndices = _indices };
            mesh.Freeze();
            var material = new DiffuseMaterial(new SolidColorBrush(color));
            material.Freeze();
            var model = new GeometryModel3D { Geometry = mesh, Material = material, BackMaterial = material };
            model.Freeze();
            return model;
        }
    }
}
