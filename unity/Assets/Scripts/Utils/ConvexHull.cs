using System;
using System.Collections.Generic;
using MIConvexHull;
using UnityEngine;

// This is a simple wrapper for the MIConvexHull. The static method CreateConvexHull takes a Unity Mesh
// and produces a Unity Mesh with the convex hull.

public class ConvexHull {
    public static Mesh CreateConvexHull(Mesh mesh) {

        List<MIConvexHull.DefaultVertex> vertices = new List<DefaultVertex>();

        foreach (Vector3 vertex in mesh.vertices) {
            MIConvexHull.DefaultVertex mi_vertex = new MIConvexHull.DefaultVertex();
            mi_vertex.Position = new double[3] { vertex.x, vertex.y, vertex.z };
            vertices.Add(mi_vertex);
        }

        MIConvexHull.ConvexHull<
                    MIConvexHull.DefaultVertex,
                    MIConvexHull.DefaultConvexFace<MIConvexHull.DefaultVertex>> mi_hull =
            MIConvexHull.ConvexHull.Create(vertices);

        List<Vector3> hull_vertices = new List<Vector3>();
        foreach (MIConvexHull.DefaultVertex vertex in mi_hull.Points) {
            hull_vertices.Add(new Vector3((float)vertex.Position[0], (float)vertex.Position[1], (float)vertex.Position[2]));
        }

        IList<MIConvexHull.DefaultVertex> mi_hull_vertices = new List<MIConvexHull.DefaultVertex>(mi_hull.Points);
        List<int> triangles = new List<int>();
        foreach (MIConvexHull.DefaultConvexFace<MIConvexHull.DefaultVertex> face in mi_hull.Faces) {
            triangles.Add(mi_hull_vertices.IndexOf(face.Vertices[0]));
            triangles.Add(mi_hull_vertices.IndexOf(face.Vertices[1]));
            triangles.Add(mi_hull_vertices.IndexOf(face.Vertices[2]));
        }

        Mesh hull = new Mesh();
        hull.vertices = hull_vertices.ToArray();
        hull.triangles = triangles.ToArray();
        hull.RecalculateNormals();

        return hull;
    }
}

