#pragma warning disable 0219

using UnityEngine;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Parabox.STL {
    /**
	 * Import methods for STL files.
	 */
    public static class pb_Stl_Importer {
        const int MAX_FACETS_PER_MESH = 65535 / 3;

        class Facet {
            public Vector3 normal;
            public Vector3 a, b, c;

            public override string ToString() {
                return string.Format("{0:F2}: {1:F2}, {2:F2}, {3:F2}", normal, a, b, c);
            }
        }

        /**
		 * Import an STL file at path.
		 */
        public static List<Mesh> Import(string path) {
            if (IsBinary(path)) {
                try {
                    return ImportBinary(path);
                } catch (System.Exception e) {
                    Logger.Error("pb_Stl_Importer::ImportBinary::Failed importing mesh at path {0} -- {1}", path, e.Message);
                    return null;
                }
            } else {
                return ImportAscii(path);
            }
        }

        private static List<Mesh> ImportBinary(string path) {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                return ImportBinaryStream(fs);
            }
        }

        public static List<Mesh> ImportBytes(byte[] bytes) {
            using (MemoryStream ms = new MemoryStream(bytes)) {
                return ImportBinaryStream(ms);
            }
        }

        private static List<Mesh> ImportBinaryStream(Stream stream) {
            Facet[] facets;
            using (BinaryReader br = new BinaryReader(stream, new ASCIIEncoding())) {
                // read header
                byte[] header = br.ReadBytes(80);
                uint facetCount = br.ReadUInt32();
                facets = new Facet[facetCount];

                for (uint i = 0; i < facetCount; i++)
                    facets[i] = br.GetFacet();
            }
            return CreateMeshWithFacets(facets);
        }

        private static Facet GetFacet(this BinaryReader binaryReader) {
            Facet facet = new Facet();
            facet.normal = binaryReader.GetVector3();

            // maintain counter-clockwise orientation of vertices:
            facet.a = binaryReader.GetVector3();
            facet.b = binaryReader.GetVector3();
            facet.c = binaryReader.GetVector3();

            if (facet.normal.magnitude < 0.01f) {
                facet.normal = CalculateNormal(facet.a, facet.b, facet.c);
            }

            binaryReader.ReadUInt16(); // padding

            return facet;
        }

        private static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c) {
            return Vector3.Cross(b - a, c - a).normalized;
        }

        private static Vector3 GetVector3(this BinaryReader binaryReader) {
            Vector3 vector3 = new Vector3();
            for (int i = 0; i < 3; i++)
                vector3[i] = binaryReader.ReadSingle();
            return vector3.UnityCoordTrafo();
        }

        private static Vector3 UnityCoordTrafo(this Vector3 vector3) {
            return new Vector3(vector3.x, vector3.y, vector3.z);
        }

        const int SOLID = 1;
        const int FACET = 2;
        const int OUTER = 3;
        const int VERTEX = 4;
        const int ENDLOOP = 5;
        const int ENDFACET = 6;
        const int ENDSOLID = 7;
        const int EMPTY = 0;

        private static int ReadState(string line) {
            if (line.StartsWith("solid"))
                return SOLID;
            else if (line.StartsWith("facet"))
                return FACET;
            else if (line.StartsWith("outer"))
                return OUTER;
            else if (line.StartsWith("vertex"))
                return VERTEX;
            else if (line.StartsWith("endloop"))
                return ENDLOOP;
            else if (line.StartsWith("endfacet"))
                return ENDFACET;
            else if (line.StartsWith("endsolid"))
                return ENDSOLID;
            else
                return EMPTY;
        }

        private static List<Mesh> ImportAscii(string path) {
            List<Facet> facets = new List<Facet>();

            using (StreamReader sr = new StreamReader(path)) {
                string line;
                int state = EMPTY, vertex = 0;
                Facet f = null;
                bool exit = false;

                while (sr.Peek() > 0 && !exit) {
                    line = sr.ReadLine().Trim();
                    int previousState = state;
                    state = ReadState(line);

                    switch (state) {
                    case SOLID:
                        continue;

                    case FACET:
                        f = new Facet();
                        f.normal = StringToVec3(line.Replace("facet normal ", ""));
                        break;

                    case OUTER:
                        vertex = 0;
                        break;

                    case VERTEX:
                        // maintain counter-clockwise orientation of vertices:
                        if (vertex == 0) f.a = StringToVec3(line.Replace("vertex ", ""));
                        else if (vertex == 2) f.c = StringToVec3(line.Replace("vertex ", ""));
                        else if (vertex == 1) f.b = StringToVec3(line.Replace("vertex ", ""));
                        vertex++;
                        break;

                    case ENDLOOP:
                        break;

                    case ENDFACET:
                        facets.Add(f);
                        break;

                    case ENDSOLID:
                        exit = true;
                        break;

                    case EMPTY:
                    default:
                        break;

                    }
                }
            }

            return CreateMeshWithFacets(facets);
        }

        private static Vector3 StringToVec3(string str) {
            string[] split = str.Trim().Split(null);
            Vector3 v = new Vector3();

            float.TryParse(split[0], out v.x);
            float.TryParse(split[1], out v.y);
            float.TryParse(split[2], out v.z);

            return v.UnityCoordTrafo();
        }

        /**
		 * Read the first 80 bytes of a file and if they are all 0x0 it's likely
		 * that this file is binary.
		 */
        private static bool IsBinary(string path) {
            // http://stackoverflow.com/questions/968935/compare-binary-files-in-c-sharp
            FileInfo file = new FileInfo(path);

            if (file.Length < 130)
                return false;

            var isBinary = false;

            using (FileStream f0 = file.OpenRead()) {
                using (BufferedStream bs0 = new BufferedStream(f0)) {
                    for (long i = 0; i < 80; i++) {
                        var readByte = bs0.ReadByte();
                        if (readByte == 0x0) {
                            isBinary = true;
                            break;
                        }
                    }
                }
            }

            if (!isBinary) {
                using (FileStream f0 = file.OpenRead()) {
                    using (BufferedStream bs0 = new BufferedStream(f0)) {
                        var byteArray = new byte[6];

                        for (var i = 0; i < 6; i++) {
                            byteArray[i] = (byte)bs0.ReadByte();
                        }

                        var text = Encoding.UTF8.GetString(byteArray);
                        isBinary = text != "solid ";
                    }
                }
            }

            return isBinary;
        }

        private static int MeshesCount(int facets) {
            return (facets + MAX_FACETS_PER_MESH - 1) / MAX_FACETS_PER_MESH;
        }

        /**
		 * @todo test with > USHORT_MAX vertex count meshes
		 */
        private static List<Mesh> CreateMeshWithFacets(IList<Facet> facets) {
            int facet_count = facets.Count, current_facet = 0, full_mesh_vertices = MAX_FACETS_PER_MESH * 3;
            List<Mesh> meshes = new List<Mesh>();

            Vector3 center = Vector3.zero;
            foreach (Facet facet in facets) {
                center += facet.a + facet.b + facet.c;
            }
            center /= facets.Count * 3.0f;

            for (int i = 0; i < MeshesCount(facets.Count); i++) {
                int len = System.Math.Min(full_mesh_vertices, (facet_count - current_facet) * 3);

                Vector3[] v = new Vector3[len];
                Vector3[] n = new Vector3[len];
                Vector2[] uv = new Vector2[len];
                int[] t = new int[len];

                Dictionary<Vector3, List<Vector3>> normals = new Dictionary<Vector3, List<Vector3>>();

                int facet_index = current_facet;

                for (int it = 0; it < len; it += 3) {
                    v[it] = facets[facet_index].a;
                    v[it + 1] = facets[facet_index].b;
                    v[it + 2] = facets[facet_index].c;

                    AddNormal(normals, facets[facet_index].a, facets[facet_index].normal.normalized);
                    AddNormal(normals, facets[facet_index].b, facets[facet_index].normal.normalized);
                    AddNormal(normals, facets[facet_index].c, facets[facet_index].normal.normalized);
                     
                    t[it] = it;
                    t[it + 1] = it + 1;
                    t[it + 2] = it + 2;

                    uv[it] = GetUv(facets[facet_index].a - center);
                    uv[it + 1] = GetUv(facets[facet_index].b - center);
                    uv[it + 2] = GetUv(facets[facet_index].c - center);

                    facet_index++;
                }

                facet_index = current_facet;

                for (int it = 0; it < len; it += 3) {
                    n[it] = CalculateNormal(facets[facet_index].normal, normals[facets[facet_index].a]);
                    n[it + 1] = CalculateNormal(facets[facet_index].normal, normals[facets[facet_index].b]);
                    n[it + 2] = CalculateNormal(facets[facet_index].normal, normals[facets[facet_index].c]);
                    facet_index++;
                }

                current_facet = facet_index;

                Mesh mesh = new Mesh();
                mesh.vertices = v;
                mesh.normals = n;
                mesh.triangles = t;
                mesh.uv = uv;
                meshes.Add(mesh);
            }

            return meshes;
        }

        private static void AddNormal(Dictionary<Vector3, List<Vector3>> dictionary, Vector3 vertex, Vector3 normal) {
            if (dictionary.ContainsKey(vertex)) {
                dictionary[vertex].Add(normal);
            } else {
                dictionary[vertex] = new List<Vector3>();
                dictionary[vertex].Add(normal);
            }
        }

        private static Vector3 CalculateNormal(Vector3 base_normal, List<Vector3> normal_list) {
            Vector3 sum = Vector3.zero;
            int sum_count = 0;
            foreach (Vector3 normal in normal_list) {
                if (Vector3.Angle(base_normal, normal) < 60.0) {
                    sum += normal;
                    sum_count++;
                }
            }
            return (sum / sum_count).normalized;
        }

        private static Vector2 GetUv(Vector3 dir) {
            return new Vector2(0.5f + Mathf.Atan2(dir.x, dir.y) / (Mathf.PI * 2.0f), 0.5f + Mathf.Acos(dir.normalized.z) / Mathf.PI);
        }
    }
}
