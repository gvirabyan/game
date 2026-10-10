using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Corehold
{
    // Builds flat-shaded, vertex-coloured low-poly meshes from simple parts.
    // Every face gets its own vertices so lighting shows crisp facets.
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        // Applied to every part added after it is set; lets callers place whole groups of parts.
        public Matrix4x4 Transform = Matrix4x4.identity;

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            a = Transform.MultiplyPoint3x4(a);
            b = Transform.MultiplyPoint3x4(b);
            c = Transform.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        // Corners in clockwise order when looking at the visible side (Unity's front face).
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Triangle(a, b, c, color);
            Triangle(a, c, d, color);
        }

        // Axis-aligned box (before rotation) around center; yaw rotates around the vertical axis in degrees.
        public void Box(Vector3 center, Vector3 size, Color color, float yaw = 0f, bool bottom = false, Color? top = null)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + r * new Vector3(x * h.x, y * h.y, z * h.z);
            var p000 = P(-1, -1, -1); var p100 = P(1, -1, -1); var p010 = P(-1, 1, -1); var p110 = P(1, 1, -1);
            var p001 = P(-1, -1, 1); var p101 = P(1, -1, 1); var p011 = P(-1, 1, 1); var p111 = P(1, 1, 1);
            var shade = Shade(color, 0.86f);
            Quad(p010, p011, p111, p110, top ?? color);    // top
            Quad(p000, p010, p110, p100, shade);           // -z
            Quad(p101, p111, p011, p001, shade);           // +z
            Quad(p001, p011, p010, p000, shade);           // -x
            Quad(p100, p110, p111, p101, shade);           // +x
            if (bottom) Quad(p000, p100, p101, p001, shade);
        }

        // Vertical prism (cylinder) standing on baseCenter.
        public void Cylinder(Vector3 baseCenter, float radius, float height, int segments, Color color, Color? top = null, float topRadius = -1f)
        {
            if (topRadius < 0f) topRadius = radius;
            var up = new Vector3(0f, height, 0f);
            var side = Shade(color, 0.88f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var b0 = baseCenter + d0 * radius; var b1 = baseCenter + d1 * radius;
                var t0 = baseCenter + up + d0 * topRadius; var t1 = baseCenter + up + d1 * topRadius;
                Quad(b0, t0, t1, b1, side);
                if (topRadius > 0f) Triangle(baseCenter + up, t1, t0, top ?? color);
            }
        }

        // Horizontal cylinder along +X from start, e.g. gun barrels and missiles.
        public void Barrel(Vector3 start, float length, float radius, int segments, Color color, float yaw = 0f, float pitch = 0f)
        {
            var r = Quaternion.Euler(0f, yaw, pitch);
            var side = Shade(color, 0.9f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var d0 = new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var d1 = new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                var e = new Vector3(length, 0f, 0f);
                Quad(start + r * d0, start + r * d1, start + r * (d1 + e), start + r * (d0 + e), side);
                Triangle(start + r * e, start + r * (d0 + e), start + r * (d1 + e), color);
            }
        }

        // Low-poly sphere; jitter > 0 roughens it (rocks, bushes).
        public void Sphere(Vector3 center, Vector3 radius, Color color, int rings = 4, int sectors = 7, float jitter = 0f, int seed = 0)
        {
            var random = new System.Random(seed);
            var grid = new Vector3[rings + 1, sectors];
            for (int y = 0; y <= rings; y++)
            {
                float lat = Mathf.PI * y / rings - Mathf.PI * 0.5f;
                for (int x = 0; x < sectors; x++)
                {
                    float lon = Mathf.PI * 2f * x / sectors + (y % 2) * Mathf.PI / sectors;
                    var d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    float k = 1f + (jitter > 0f && y > 0 && y < rings ? ((float)random.NextDouble() - 0.5f) * 2f * jitter : 0f);
                    grid[y, x] = center + Vector3.Scale(d * k, radius);
                }
            }
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < sectors; x++)
                {
                    int x1 = (x + 1) % sectors;
                    var c = Shade(color, 0.92f + 0.08f * y / rings);
                    if (y > 0) Triangle(grid[y, x], grid[y + 1, x], grid[y, x1], c);
                    if (y < rings - 1) Triangle(grid[y, x1], grid[y + 1, x], grid[y + 1, x1], c);
                }
            }
        }

        // Flat disc lying on the ground (y = height).
        public void Disc(Vector3 center, float radius, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Triangle(center, center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius,
                    center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, color);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            // Colours are authored in sRGB; vertex colours are not converted by Unity in linear colour space.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < colors.Count; i++)
                {
                    var c = colors[i];
                    var lin = c.linear;
                    colors[i] = new Color(lin.r, lin.g, lin.b, c.a);
                }
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
