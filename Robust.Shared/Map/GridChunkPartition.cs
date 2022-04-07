using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Robust.Shared.Map
{
    /// <summary>
    /// Partitions a grid chunk into a set of non-overlapping rectangles.
    /// </summary>
    internal static class GridChunkPartition
    {
        /// <summary>
        /// Iterates through a chunk row and tries to find the first valid polygon tile.
        /// </summary>
        private static void GetRowPolygons(List<List<Vector2i>> polys, ushort row, MapChunk chunk)
        {
            var running = false;
            ushort origin = default;
            //var polys = new List<List<Vector2i>>();
            //var fakePolys = new List<Box2i>();
            var tileDefManager = IoCManager.Resolve<ITileDefinitionManager>();

            for (ushort x = 0; x < chunk.ChunkSize; x++)
            {
                var tile = chunk.GetTile(x, row);

                if (!running)
                {
                    if (!CanStart(tile)) continue;

                    running = true;
                    origin = x;
                    // Iterate the tile again and try to end on it.
                    x--;
                    continue;
                }

                if (!TryEnd(tile.IsEmpty ? TileFlag.None : tile.Flags, out var index) && x != chunk.ChunkSize - 1) continue;

                running = false;
                var polygon = new List<Vector2i>(4);
                var endIndex = (ushort) (index + x);

                var originTile = chunk.GetTile(origin, row);
                var originCollision = originTile.Flags;

                var endTile = chunk.GetTile(endIndex, row);
                var endCollision = endTile.Flags;

                // Grab:
                // Origin bot left
                // End bot right
                // End top right
                // Origin top left
                polygon.Add(GetVertex(originCollision, 0, origin, row));
                polygon.Add(GetVertex(endCollision, 1, endIndex, row));
                polygon.Add(GetVertex(endCollision, 2, endIndex, row));
                polygon.Add(GetVertex(originCollision, 3, origin, row));

                // De-dupe verts: Should only ever be 1 duplicated for triangles.
                for (var i = 0; i < polygon.Count; i++)
                {
                    var vert = polygon[i];
                    var nextVert = polygon[(i + 1) % polygon.Count];
                    if (vert.Equals(nextVert))
                    {
                        polygon.RemoveAt(i);
                        break;
                    }
                }

                polys.Add(polygon);
            }
        }

        /// <summary>
        /// Tries to get a vertex with the specified index.
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="index"></param>
        /// <param name="x">Left index of the tile</param>
        /// <param name="y">Bottom index of the tile</param>
        /// <returns></returns>
        private static Vector2i GetVertex(TileFlag flag, int index, int x, int y)
        {
            return index switch
            {
                0 => flag switch
                {
                    TileFlag.Full => new Vector2i(x, y),
                    TileFlag.BottomLeft => new Vector2i(x, y),
                    TileFlag.BottomRight => new Vector2i(x, y),
                    TileFlag.TopRight => new Vector2i(x + 1, y),
                    TileFlag.TopLeft => new Vector2i(x, y),
                    _ => throw new NotImplementedException()
                },
                1 => flag switch
                {
                    TileFlag.Full => new Vector2i(x + 1, y),
                    TileFlag.BottomLeft => new Vector2i(x + 1, y),
                    TileFlag.BottomRight => new Vector2i(x + 1, y),
                    TileFlag.TopRight => new Vector2i(x + 1, y),
                    TileFlag.TopLeft => new Vector2i(x + 1, y + 1),
                    _ => throw new NotImplementedException()
                },
                2 => flag switch
                {
                    TileFlag.Full => new Vector2i(x + 1, y + 1),
                    TileFlag.BottomLeft => new Vector2i(x, y + 1),
                    TileFlag.BottomRight => new Vector2i(x + 1, y + 1),
                    TileFlag.TopRight => new Vector2i(x + 1, y + 1),
                    TileFlag.TopLeft => new Vector2i(x + 1, y + 1),
                    _ => throw new NotImplementedException()
                },
                3 => flag switch
                {
                    TileFlag.Full => new Vector2i(x, y + 1),
                    TileFlag.BottomLeft => new Vector2i(x, y + 1),
                    TileFlag.BottomRight => new Vector2i(x, y),
                    TileFlag.TopRight => new Vector2i(x, y + 1),
                    TileFlag.TopLeft => new Vector2i(x, y + 1),
                    _ => throw new NotImplementedException()
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static bool CanStart(Tile tile)
        {
            if (tile.IsEmpty) return false;

            return true;
        }

        private static bool TryEnd(TileFlag flag, out int index)
        {
            // Tries to terminate the tile.
            // If the tile is empty will return the preceding tile as the terminator
            switch (flag)
            {
                case TileFlag.None:
                    index = -1;
                    return true;
                case TileFlag.Full:
                    index = 0;
                    return false;
                case TileFlag.BottomLeft:
                    index = 0;
                    return true;
                case TileFlag.BottomRight:
                    index = 0;
                    return false;
                case TileFlag.TopRight:
                    index = 0;
                    return false;
                case TileFlag.TopLeft:
                    index = 0;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void PartitionChunk(MapChunk chunk, out Box2i bounds, out List<List<Vector2i>> polygons)
        {
            polygons = new List<List<Vector2i>>();

            for (ushort y = 0; y < chunk.ChunkSize; y++)
            {
                GetRowPolygons(polygons, y, chunk);
            }

            // Patch them together as available (TODO NOW
            PatchPolygons(polygons);

            bounds = new Box2i();
            var minimum = Vector2i.Zero;
            var maximum = Vector2i.Zero;

            foreach (var poly in polygons)
            {
                foreach (var vert in poly)
                {
                    minimum = Vector2i.ComponentMin(minimum, vert);
                    maximum = Vector2i.ComponentMax(maximum, vert);
                }
            }

            bounds = new Box2i(minimum, maximum);
        }

        /// <summary>
        /// Attempt to combine polygons together to reduce their count.
        /// </summary>
        private static void PatchPolygons(List<List<Vector2i>> polygons)
        {
            var combined = true;

            while (combined)
            {
                combined = false;

                for (var i = polygons.Count - 1; i >= 0; i--)
                {
                    var polyA = polygons[i];

                    for (var j = i - 1; j >= 0; j--)
                    {
                        var polyB = polygons[j];

                        var dupe = 0;
                        foreach (var vertA in polyA)
                        {
                            foreach (var vertB in polyB)
                            {
                                if (!vertA.Equals(vertB)) continue;
                                dupe++;
                            }
                        }

                        if (dupe < 2) continue;

                        var combinedPoly = CombinePolygons(polyA, polyB);

                        if (!IsConvex(combinedPoly)) continue;

                        // Replace polyA and remove polyB.
                        polygons[i] = combinedPoly;
                        polygons.RemoveAt(j);
                        combined = true;
                    }
                }
            }
        }

        private static List<Vector2i> CombinePolygons(List<Vector2i> polyA, List<Vector2i> polyB)
        {
            // TODO: Need a test for this real bad.
            // TODO: Lord this is disgusting.
            var set = new HashSet<Vector2i>(polyA);
            set.EnsureCapacity(polyA.Count + polyB.Count - 2);
            set.UnionWith(polyB);

            Span<Vector2> spin = stackalloc Vector2[set.Count];
            var index = 0;

            foreach (var vert in set)
            {
                spin[index] = vert;
                index++;
            }

            var wrapped = GiftWrap.SetConvexHull(spin, spin.Length);
            var list = new List<Vector2i>(wrapped.Length);

            foreach (var vec in wrapped)
                list.Add((Vector2i) vec);

            return list;
        }

        private static bool IsConvex(List<Vector2i> polygon)
        {
            // Also considering max verts allowed in box2d.
            if (polygon.Count < 3 || polygon.Count > 8) return false;
            // https://stackoverflow.com/questions/471962/how-do-i-efficiently-determine-if-a-polygon-is-convex-non-convex-or-complex/45372025#45372025

            var (oldX, oldY) = polygon[^2];
            var (newX, newY) = polygon[^1];
            var newDirection = MathF.Atan2(newY - oldY, newX - oldX);
            var oldDirection = 0f;
            float orientation = 0f;
            var angleSum = 0f;

            for (var i = 0; i < polygon.Count; i++)
            {
                var newPoint = polygon[i];
                oldX = newX;
                oldY = newY;
                oldDirection = newDirection;
                (newX, newY) = newPoint;
                newDirection = MathF.Atan2(newY - oldY, newX - oldX);

                if (oldX.Equals(newX) && oldY.Equals(newY))
                    return false;

                var angle = newDirection - oldDirection;
                if (angle <= -MathF.PI)
                    angle += MathF.Tau;
                else if (angle > MathF.PI)
                    angle -= MathF.Tau;
                if (i == 0)
                {
                    if (angle == 0f)
                        return false;

                    orientation = angle > 0f ? 1f : -1f;
                }
                else
                {
                    if (orientation * angle < 0f)
                        return false;
                }

                angleSum += angle;
            }

            return MathF.Abs(MathF.Round(angleSum / MathF.Tau)).Equals(1f);
        }
    }
}
