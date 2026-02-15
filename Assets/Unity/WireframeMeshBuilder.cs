using UnityEngine;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Builds a procedural wireframe cube mesh from 12 thin rectangular prism edges.
    /// Mobile-compatible (no geometry shader needed). Single mesh, ~288 verts, ~432 tris.
    /// </summary>
    public static class WireframeMeshBuilder
    {
        /// <summary>
        /// Build a wireframe cube mesh of the given size with edges of the given thickness.
        /// </summary>
        public static Mesh Build(float size = 1f, float thickness = 0.02f)
        {
            float half = size * 0.5f;
            float t = thickness * 0.5f;

            // A cube has 12 edges: 4 along X, 4 along Y, 4 along Z
            // For each edge, we create a thin box (rectangular prism)
            var combine = new CombineInstance[12];
            int i = 0;

            // 4 edges along X axis (at each combination of Y and Z corners)
            combine[i++] = MakeEdge(new Vector3(0, -half, -half), Vector3.right, size, t);
            combine[i++] = MakeEdge(new Vector3(0, -half,  half), Vector3.right, size, t);
            combine[i++] = MakeEdge(new Vector3(0,  half, -half), Vector3.right, size, t);
            combine[i++] = MakeEdge(new Vector3(0,  half,  half), Vector3.right, size, t);

            // 4 edges along Y axis
            combine[i++] = MakeEdge(new Vector3(-half, 0, -half), Vector3.up, size, t);
            combine[i++] = MakeEdge(new Vector3(-half, 0,  half), Vector3.up, size, t);
            combine[i++] = MakeEdge(new Vector3( half, 0, -half), Vector3.up, size, t);
            combine[i++] = MakeEdge(new Vector3( half, 0,  half), Vector3.up, size, t);

            // 4 edges along Z axis
            combine[i++] = MakeEdge(new Vector3(-half, -half, 0), Vector3.forward, size, t);
            combine[i++] = MakeEdge(new Vector3(-half,  half, 0), Vector3.forward, size, t);
            combine[i++] = MakeEdge(new Vector3( half, -half, 0), Vector3.forward, size, t);
            combine[i++] = MakeEdge(new Vector3( half,  half, 0), Vector3.forward, size, t);

            var mesh = new Mesh { name = "WireframeCube" };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            // Clean up the temporary edge meshes
            for (int j = 0; j < combine.Length; j++)
                Object.Destroy(combine[j].mesh);

            return mesh;
        }

        private static CombineInstance MakeEdge(Vector3 center, Vector3 direction, float length, float thickness)
        {
            // Build a box mesh for one edge
            var mesh = MakeBoxMesh(direction, length, thickness);
            return new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.Translate(center)
            };
        }

        private static Mesh MakeBoxMesh(Vector3 direction, float length, float thickness)
        {
            // Determine half-extents for the box along each axis
            float hx, hy, hz;

            if (direction == Vector3.right)
            {
                hx = length * 0.5f;
                hy = thickness;
                hz = thickness;
            }
            else if (direction == Vector3.up)
            {
                hx = thickness;
                hy = length * 0.5f;
                hz = thickness;
            }
            else // forward
            {
                hx = thickness;
                hy = thickness;
                hz = length * 0.5f;
            }

            var verts = new Vector3[]
            {
                // Front face (z+)
                new Vector3(-hx, -hy,  hz),
                new Vector3( hx, -hy,  hz),
                new Vector3( hx,  hy,  hz),
                new Vector3(-hx,  hy,  hz),
                // Back face (z-)
                new Vector3( hx, -hy, -hz),
                new Vector3(-hx, -hy, -hz),
                new Vector3(-hx,  hy, -hz),
                new Vector3( hx,  hy, -hz),
                // Top face (y+)
                new Vector3(-hx,  hy,  hz),
                new Vector3( hx,  hy,  hz),
                new Vector3( hx,  hy, -hz),
                new Vector3(-hx,  hy, -hz),
                // Bottom face (y-)
                new Vector3(-hx, -hy, -hz),
                new Vector3( hx, -hy, -hz),
                new Vector3( hx, -hy,  hz),
                new Vector3(-hx, -hy,  hz),
                // Right face (x+)
                new Vector3( hx, -hy,  hz),
                new Vector3( hx, -hy, -hz),
                new Vector3( hx,  hy, -hz),
                new Vector3( hx,  hy,  hz),
                // Left face (x-)
                new Vector3(-hx, -hy, -hz),
                new Vector3(-hx, -hy,  hz),
                new Vector3(-hx,  hy,  hz),
                new Vector3(-hx,  hy, -hz),
            };

            var tris = new int[]
            {
                 0,  2,  1,  0,  3,  2,  // front
                 4,  6,  5,  4,  7,  6,  // back
                 8, 10,  9,  8, 11, 10,  // top
                12, 14, 13, 12, 15, 14,  // bottom
                16, 18, 17, 16, 19, 18,  // right
                20, 22, 21, 20, 23, 22,  // left
            };

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            return mesh;
        }
    }
}
