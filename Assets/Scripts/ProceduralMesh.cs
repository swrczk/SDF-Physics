using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public static class ProceduralMesh
    {
        public static Mesh CreateLowPolySphere(int rings, int segments, float radius)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (var ring = 0; ring <= rings; ring++)
            {
                var phi = (float)ring / rings * Mathf.PI; // 0 (biegun góra) .. PI (biegun dół)

                for (var seg = 0; seg <= segments; seg++)
                {
                    var theta = (float)seg / segments * Mathf.PI * 2f; // 0 .. 2*PI dookoła

                    var x = Mathf.Sin(phi) * Mathf.Cos(theta);
                    var y = Mathf.Cos(phi);
                    var z = Mathf.Sin(phi) * Mathf.Sin(theta);

                    vertices.Add(new Vector3(x, y, z) * radius);
                }
            }

            var vertsPerRing = segments + 1;
            for (var ring = 0; ring < rings; ring++)
            {
                for (var seg = 0; seg < segments; seg++)
                {
                    var current = ring * vertsPerRing + seg;
                    var next = current + vertsPerRing;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}