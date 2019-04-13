﻿using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using System;
using JetBrains.Annotations;

namespace Robust.Shared.Map
{
    /// <summary>
    ///     Coordinates relative to a specific grid.
    /// </summary>
    [Serializable, NetSerializable]
    public readonly struct GridCoordinates : IEquatable<GridCoordinates>
    {
        public readonly GridId GridID;
        public readonly Vector2 Position;

        public float X => Position.X;

        public float Y => Position.Y;

        public IMapGrid Grid => IoCManager.Resolve<IMapManager>().GetGrid(GridID);

        public static readonly GridCoordinates Nullspace = new GridCoordinates(0, 0, GridId.Nullspace);

        /// <summary>
        ///     The map the grid is currently on. This value is not persistent and may change!
        /// </summary>
        public IMap Map => Grid.Map;

        /// <summary>
        ///     The map ID the grid is currently on. This value is not persistent and may change!
        /// </summary>
        public MapId MapID => Map.Index;

        /// <summary>
        ///     True if these coordinates are relative to a map itself.
        /// </summary>
        public bool IsWorld
        {
            get
            {
                var grid = Grid;
                return grid == grid.Map.DefaultGrid;
            }
        }

        public GridCoordinates(Vector2 argPosition, IMapGrid argGrid)
        {
            Position = argPosition;
            GridID = argGrid.Index;
        }

        public GridCoordinates(Vector2 argPosition, GridId argGrid)
        {
            Position = argPosition;
            GridID = argGrid;
        }

        /// <summary>
        ///     Construct new grid local coordinates relative to the default grid of a map.
        /// </summary>
        public GridCoordinates(Vector2 argPosition, MapId argMap)
        {
            Position = argPosition;
            var mapManager = IoCManager.Resolve<IMapManager>();
            GridID = mapManager.GetMap(argMap).DefaultGrid.Index;
        }

        /// <summary>
        ///     Construct new grid local coordinates relative to the default grid of a map.
        /// </summary>
        public GridCoordinates(Vector2 argPosition, IMap argMap)
        {
            Position = argPosition;
            GridID = argMap.DefaultGrid.Index;
        }

        public GridCoordinates(float X, float Y, IMapGrid argGrid)
        {
            Position = new Vector2(X, Y);
            GridID = argGrid.Index;
        }

        public GridCoordinates(float X, float Y, GridId argGrid)
        {
            Position = new Vector2(X, Y);
            GridID = argGrid;
        }

        /// <summary>
        ///     Construct new grid local coordinates relative to the default grid of a map.
        /// </summary>
        public GridCoordinates(float X, float Y, MapId argMap) : this(new Vector2(X, Y), argMap)
        {
        }

        /// <summary>
        ///     Construct new grid local coordinates relative to the default grid of a map.
        /// </summary>
        public GridCoordinates(float X, float Y, IMap argMap) : this(new Vector2(X, Y), argMap)
        {
        }

        public bool IsValidLocation()
        {
            var mapMan = IoCManager.Resolve<IMapManager>();
            return mapMan.GridExists(GridID);
        }

        public GridCoordinates ConvertToGrid(IMapGrid argGrid)
        {
            return new GridCoordinates(Position + Grid.WorldPosition - argGrid.WorldPosition, argGrid);
        }

        public GridCoordinates ToWorld()
        {
            return ConvertToGrid(Map.DefaultGrid);
        }

        public GridCoordinates Offset(Vector2 offset)
        {
            return new GridCoordinates(Position + offset, GridID);
        }

        public bool InRange(GridCoordinates localpos, float range)
        {
            if (localpos.MapID != MapID)
            {
                return false;
            }

            return ((localpos.ToWorld().Position - ToWorld().Position).LengthSquared < range * range);
        }

        public bool InRange(GridCoordinates localpos, int range)
        {
            return InRange(localpos, (float) range);
        }

        public float Distance(GridCoordinates other)
        {
            return (ToWorld().Position - other.ToWorld().Position).Length;
        }

        public GridCoordinates Translated(Vector2 offset)
        {
            return new GridCoordinates(Position + offset, GridID);
        }

        public override string ToString()
        {
            return $"Grid={GridID}, X={Position.X:N2}, Y={Position.Y:N2}";
        }

        public bool Equals(GridCoordinates other)
        {
            return GridID.Equals(other.GridID) && Position.Equals(other.Position);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is GridCoordinates && Equals((GridCoordinates) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = GridID.GetHashCode();
                hashCode = (hashCode * 397) ^ Position.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        ///     Tests for value equality between two LocalCoordinates.
        /// </summary>
        public static bool operator ==(GridCoordinates self, GridCoordinates other)
        {
            return self.Equals(other);
        }

        /// <summary>
        ///     Tests for value inequality between two LocalCoordinates.
        /// </summary>
        public static bool operator !=(GridCoordinates self, GridCoordinates other)
        {
            return !(self == other);
        }
    }

    [Serializable, NetSerializable]
    public readonly struct ScreenCoordinates
    {
        public readonly Vector2 Position;

        public float X => Position.X;

        public float Y => Position.Y;

        public ScreenCoordinates(Vector2 argPosition)
        {
            Position = argPosition;
        }

        public ScreenCoordinates(float x, float y)
        {
            Position = new Vector2(x, y);
        }

        public override string ToString()
        {
            return Position.ToString();
        }
    }

    /// <summary>
    ///     Coordinates relative to a specific map.
    /// </summary>
    [PublicAPI]
    [Serializable, NetSerializable]
    public readonly struct MapCoordinates : IEquatable<MapCoordinates>
    {
        /// <summary>
        ///     World Position coordinates.
        /// </summary>
        public readonly Vector2 Position;

        /// <summary>
        ///     Map identifier relevant to this position.
        /// </summary>
        public readonly MapId MapId;

        /// <summary>
        ///     World position on the X axis.
        /// </summary>
        public float X => Position.X;

        /// <summary>
        ///     World position on the Y axis.
        /// </summary>
        public float Y => Position.Y;

        /// <summary>
        ///     Constructs a new instance of <c>MapCoordinates</c>.
        /// </summary>
        /// <param name="position">World position coordinates.</param>
        /// <param name="mapId">Map identifier relevant to this position.</param>
        public MapCoordinates(Vector2 position, MapId mapId)
        {
            Position = position;
            MapId = mapId;
        }

        /// <summary>
        ///     Constructs a new instance of <c>MapCoordinates</c>.
        /// </summary>
        /// <param name="x">World position coordinate on the X axis.</param>
        /// <param name="y">World position coordinate on the Y axis.</param>
        /// <param name="mapId">Map identifier relevant to this position.</param>
        public MapCoordinates(float x, float y, MapId mapId)
            : this(new Vector2(x, y), mapId) { }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"({Position.X}, {Position.Y}, map: {MapId})";
        }

        /// <inheritdoc />
        public bool Equals(MapCoordinates other)
        {
            return Position.Equals(other.Position) && MapId.Equals(other.MapId);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is MapCoordinates other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ MapId.GetHashCode();
            }
        }

        /// <summary>
        ///     Check for equality by value between two objects.
        /// </summary>
        public static bool operator ==(MapCoordinates a, MapCoordinates b)
        {
            return a.Equals(b);
        }

        /// <summary>
        ///     Check for inequality by value between two objects.
        /// </summary>
        public static bool operator !=(MapCoordinates a, MapCoordinates b)
        {
            return !a.Equals(b);
        }
    }
}
