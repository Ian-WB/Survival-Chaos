using System.Collections.Generic;
using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// How thick each separate part of a mesh is, for the far trees and ruins
    /// (environment roadmap, item 47, 8 October 2026).
    ///
    /// The two kinds of scenery are built differently, so there are two
    /// measures.
    ///
    /// A ruin is a wall with each window bar a box of its own. It comes
    /// apart into its parts by asking which triangles share a corner, and a
    /// part's thickness is the middle one of its three sizes: a bar is long
    /// one way and thin the other two, where a wall is thin only one way
    /// (<see cref="Measure"/>).
    ///
    /// A tree is one skin, trunk and branches together, so it has no parts
    /// to take apart. Its thickness is asked all over it instead: how far
    /// it is through the wood to the other side (<see cref="Through"/>).
    /// That is the trunk's width on the trunk and falls away along a branch
    /// to nothing at its tip.
    ///
    /// The thickness goes into the mesh at import, and the scenery's shader
    /// leaves out whatever is thinner than a pixel at the distance it is
    /// drawn from. A piece under a pixel wide is drawn in some frames and
    /// not in others as the view turns, which against bright cloud is a
    /// crawl.
    ///
    /// This is arithmetic on arrays and knows nothing of the editor, so the
    /// tests can ask it.
    /// </summary>
    public static class ThinParts
    {
        /// <summary>Corners closer than this are one corner. The scenery's meshes are about a unit across.</summary>
        public const float Weld = 1e-4f;

        /// <summary>
        /// The thickness of the part each vertex belongs to, in the mesh's own
        /// units. Returns how many parts there are.
        /// </summary>
        public static int Measure(Vector3[] vertices, int[] triangles, float[] thickness)
        {
            int count = vertices.Length;
            int[] parent = new int[count];
            for (int i = 0; i < count; i++)
            {
                parent[i] = i;
            }

            // Vertices at one place are one corner: a hard edge splits a
            // vertex in two without parting the surface.
            Dictionary<Vector3Int, int> placed = new Dictionary<Vector3Int, int>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 v = vertices[i] / Weld;
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (placed.TryGetValue(key, out int first))
                {
                    Join(parent, i, first);
                }
                else
                {
                    placed.Add(key, i);
                }
            }

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Join(parent, triangles[t], triangles[t + 1]);
                Join(parent, triangles[t], triangles[t + 2]);
            }

            Dictionary<int, List<int>> parts = new Dictionary<int, List<int>>();
            for (int i = 0; i < count; i++)
            {
                int root = Find(parent, i);
                if (!parts.TryGetValue(root, out List<int> part))
                {
                    part = new List<int>();
                    parts.Add(root, part);
                }

                part.Add(i);
            }

            foreach (List<int> part in parts.Values)
            {
                float middle = MiddleSize(vertices, part);
                foreach (int i in part)
                {
                    thickness[i] = middle;
                }
            }

            return parts.Count;
        }

        /// <summary>
        /// How far it is through the mesh at every vertex, in the mesh's own
        /// units, for a mesh that is one skin.
        ///
        /// It is measured a triangle at a time: from the middle of each,
        /// straight in, to the nearest surface met. Then no triangle is left
        /// thicker than the wood between it and the foot of the tree
        /// (<see cref="NoThickerThanTheWayThere"/>), going from triangle to
        /// triangle across the edges they share. A vertex gets the thickest
        /// of the triangles that meet at it.
        ///
        /// The order matters, and two earlier tries measured at the corners
        /// instead and got it wrong: specks were left hanging where a twig
        /// had gone, and at a pixel and a half whole trees went and left
        /// only the specks. A reading at a corner depends on which way its
        /// faces happen to lean, so round one branch some corners read
        /// thick and some thin, and a triangle is dropped as soon as one of
        /// its corners is. Measured by triangle and traced from the foot,
        /// anything thick enough to draw is joined to the trunk by
        /// triangles that are drawn too: a cap on the end of a twig, where
        /// straight in is the whole length of the twig, is still no
        /// thicker than the twig.
        ///
        /// <paramref name="up"/> is which way the tree stands, in the mesh's
        /// own axes; its foot is the thickest triangle in its lowest tenth.
        /// A triangle from which nothing is met starts at <paramref name="open"/>,
        /// which should be more than any thickness that matters.
        /// </summary>
        public static void Through(Vector3[] vertices, int[] triangles, float[] thickness, float open, Vector3 up)
        {
            int count = vertices.Length;
            int faces = triangles.Length / 3;

            // One corner for every place: a hard edge splits a vertex in two without parting the skin.
            Dictionary<Vector3Int, int> placed = new Dictionary<Vector3Int, int>(count);
            int[] corner = new int[count];
            int corners = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = vertices[i] / Weld;
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (!placed.TryGetValue(key, out int index))
                {
                    index = corners++;
                    placed.Add(key, index);
                }

                corner[i] = index;
            }

            float[] through = new float[faces];
            Vector3[] middle = new Vector3[faces];
            for (int f = 0; f < faces; f++)
            {
                Vector3 a = vertices[triangles[f * 3]], b = vertices[triangles[f * 3 + 1]], c = vertices[triangles[f * 3 + 2]];
                middle[f] = (a + b + c) / 3f;
                through[f] = open;

                Vector3 outward = Vector3.Cross(b - a, c - a);
                if (outward.sqrMagnitude < 1e-20f)
                {
                    // A sliver with no side to it: as thin as can be, and no way through for the search below.
                    through[f] = 0f;
                    continue;
                }

                Vector3 inward = -outward.normalized;
                Vector3 from = middle[f] + inward * (Weld * 10f);

                for (int other = 0; other < faces; other++)
                {
                    if (other != f && Meets(from, inward, vertices[triangles[other * 3]], vertices[triangles[other * 3 + 1]],
                            vertices[triangles[other * 3 + 2]], out float distance) && distance < through[f])
                    {
                        through[f] = distance;
                    }
                }
            }

            // Which triangles share an edge.
            Dictionary<long, int> firstAcross = new Dictionary<long, int>(faces * 3);
            List<int>[] beside = new List<int>[faces];
            for (int f = 0; f < faces; f++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = corner[triangles[f * 3 + e]], b = corner[triangles[f * 3 + (e + 1) % 3]];
                    if (a == b)
                    {
                        continue;
                    }

                    long edge = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (firstAcross.TryGetValue(edge, out int other))
                    {
                        (beside[f] ??= new List<int>()).Add(other);
                        (beside[other] ??= new List<int>()).Add(f);
                    }
                    else
                    {
                        firstAcross.Add(edge, f);
                    }
                }
            }

            // The foot of the tree: the thickest triangle in its lowest tenth.
            up = up.sqrMagnitude > 1e-12f ? up.normalized : Vector3.up;
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int f = 0; f < faces; f++)
            {
                float at = Vector3.Dot(middle[f], up);
                lowest = Mathf.Min(lowest, at);
                highest = Mathf.Max(highest, at);
            }

            int foot = -1;
            for (int f = 0; f < faces; f++)
            {
                if (Vector3.Dot(middle[f], up) <= lowest + (highest - lowest) * 0.1f && (foot < 0 || through[f] > through[foot]))
                {
                    foot = f;
                }
            }

            if (foot >= 0)
            {
                NoThickerThanTheWayThere(through, beside, foot);
            }

            float[] atCorner = new float[corners];
            for (int f = 0; f < faces; f++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int c = corner[triangles[f * 3 + e]];
                    atCorner[c] = Mathf.Max(atCorner[c], through[f]);
                }
            }

            for (int i = 0; i < count; i++)
            {
                thickness[i] = atCorner[corner[i]];
            }
        }

        /// <summary>
        /// Brings every place down to the thinnest place on the thickest way
        /// to it from <paramref name="from"/>, where <paramref name="beside"/>
        /// says which places are next to which. After it, thickness only
        /// falls on the way out, so whatever is thick enough to draw is
        /// joined to the start by places that are drawn too. A place no way
        /// reaches is cut off from the start, and gets nothing.
        /// </summary>
        public static void NoThickerThanTheWayThere(float[] thickness, List<int>[] beside, int from)
        {
            int count = thickness.Length;
            float[] reached = new float[count];
            bool[] settled = new bool[count];
            for (int i = 0; i < count; i++)
            {
                reached[i] = -1f;
            }

            reached[from] = thickness[from];

            // The widest way first: a few hundred places, so the plain search is quick enough.
            while (true)
            {
                int next = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!settled[i] && reached[i] >= 0f && (next < 0 || reached[i] > reached[next]))
                    {
                        next = i;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                settled[next] = true;
                if (beside[next] == null)
                {
                    continue;
                }

                foreach (int other in beside[next])
                {
                    float through = Mathf.Min(reached[next], thickness[other]);
                    if (!settled[other] && through > reached[other])
                    {
                        reached[other] = through;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                thickness[i] = Mathf.Max(reached[i], 0f);
            }
        }

        /// <summary>Where a ray meets a triangle, from either side of it.</summary>
        private static bool Meets(Vector3 from, Vector3 along, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            Vector3 ab = b - a, ac = c - a;
            Vector3 h = Vector3.Cross(along, ac);
            float det = Vector3.Dot(ab, h);
            if (Mathf.Abs(det) < 1e-12f)
            {
                return false;
            }

            float inverse = 1f / det;
            Vector3 s = from - a;
            float u = Vector3.Dot(s, h) * inverse;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 q = Vector3.Cross(s, ab);
            float v = Vector3.Dot(along, q) * inverse;
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            distance = Vector3.Dot(ac, q) * inverse;
            return distance > 0f;
        }

        /// <summary>
        /// The middle one of a part's three sizes, measured along its own
        /// axes and not the mesh's, so a twig that leans is as thin as one
        /// that stands.
        /// </summary>
        public static float MiddleSize(Vector3[] vertices, List<int> part)
        {
            Vector3 centre = Vector3.zero;
            foreach (int i in part)
            {
                centre += vertices[i];
            }

            centre /= part.Count;

            // How the part's corners are spread: the axes of that spread are its own axes.
            float xx = 0f, yy = 0f, zz = 0f, xy = 0f, xz = 0f, yz = 0f;
            foreach (int i in part)
            {
                Vector3 d = vertices[i] - centre;
                xx += d.x * d.x;
                yy += d.y * d.y;
                zz += d.z * d.z;
                xy += d.x * d.y;
                xz += d.x * d.z;
                yz += d.y * d.z;
            }

            Vector3[] axes = Axes(xx, yy, zz, xy, xz, yz);
            float a = Size(vertices, part, axes[0]);
            float b = Size(vertices, part, axes[1]);
            float c = Size(vertices, part, axes[2]);

            return Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
        }

        private static float Size(Vector3[] vertices, List<int> part, Vector3 axis)
        {
            float least = float.MaxValue, most = float.MinValue;
            foreach (int i in part)
            {
                float along = Vector3.Dot(vertices[i], axis);
                least = Mathf.Min(least, along);
                most = Mathf.Max(most, along);
            }

            return most - least;
        }

        /// <summary>The three axes of a symmetric spread, by turning it until nothing is left off its diagonal.</summary>
        private static Vector3[] Axes(float xx, float yy, float zz, float xy, float xz, float yz)
        {
            float[,] a = { { xx, xy, xz }, { xy, yy, yz }, { xz, yz, zz } };
            float[,] v = { { 1f, 0f, 0f }, { 0f, 1f, 0f }, { 0f, 0f, 1f } };

            for (int sweep = 0; sweep < 24; sweep++)
            {
                float off = Mathf.Abs(a[0, 1]) + Mathf.Abs(a[0, 2]) + Mathf.Abs(a[1, 2]);
                if (off < 1e-12f)
                {
                    break;
                }

                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        if (Mathf.Abs(a[p, q]) < 1e-20f)
                        {
                            continue;
                        }

                        float theta = (a[q, q] - a[p, p]) / (2f * a[p, q]);
                        float t = Mathf.Sign(theta) / (Mathf.Abs(theta) + Mathf.Sqrt(theta * theta + 1f));
                        if (theta == 0f)
                        {
                            t = 1f;
                        }

                        float c = 1f / Mathf.Sqrt(t * t + 1f);
                        float s = t * c;

                        for (int k = 0; k < 3; k++)
                        {
                            float akp = a[k, p], akq = a[k, q];
                            a[k, p] = c * akp - s * akq;
                            a[k, q] = s * akp + c * akq;
                        }

                        for (int k = 0; k < 3; k++)
                        {
                            float apk = a[p, k], aqk = a[q, k];
                            a[p, k] = c * apk - s * aqk;
                            a[q, k] = s * apk + c * aqk;
                        }

                        for (int k = 0; k < 3; k++)
                        {
                            float vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
                }
            }

            return new[]
            {
                new Vector3(v[0, 0], v[1, 0], v[2, 0]).normalized,
                new Vector3(v[0, 1], v[1, 1], v[2, 1]).normalized,
                new Vector3(v[0, 2], v[1, 2], v[2, 2]).normalized,
            };
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        private static void Join(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb)
            {
                parent[ra] = rb;
            }
        }
    }
}
