﻿using Robust.Client.Interfaces.Graphics;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

#nullable enable

namespace Robust.Client.Graphics.ClientEye
{
    /// <inheritdoc />
    public sealed class EyeManager : IEyeManager
    {
        // If you modify this make sure to edit the value in the Robust.Shared.Audio.AudioParams struct default too!
        // No I can't be bothered to make this a shared constant.
        /// <summary>
        /// Default scaling for the projection matrix.
        /// </summary>
        public const int PixelsPerMeter = 32;

#pragma warning disable 649, CS8618
        // ReSharper disable twice NotNullMemberIsNotInitialized
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IClyde _displayManager;
#pragma warning restore 649, CS8618

        // We default to this when we get set to a null eye.
        private readonly FixedEye _defaultEye = new FixedEye();

        private IEye? _currentEye;

        /// <inheritdoc />
        public IEye CurrentEye
        {
            get => _currentEye ?? _defaultEye;
            set => _currentEye = value;
        }

        /// <inheritdoc />
        public MapId CurrentMap => CurrentEye.Position.MapId;

        /// <inheritdoc />
        public Box2 GetWorldViewport()
        {
            var vpSize = _displayManager.ScreenSize;

            var topLeft = ScreenToWorld(Vector2.Zero);
            var topRight = ScreenToWorld(new Vector2(vpSize.X, 0));
            var bottomRight = ScreenToWorld(vpSize);
            var bottomLeft = ScreenToWorld(new Vector2(0, vpSize.Y));

            var left = MathHelper.Min(topLeft.X, topRight.X, bottomRight.X, bottomLeft.X);
            var bottom = MathHelper.Min(topLeft.Y, topRight.Y, bottomRight.Y, bottomLeft.Y);
            var right = MathHelper.Max(topLeft.X, topRight.X, bottomRight.X, bottomLeft.X);
            var top = MathHelper.Max(topLeft.Y, topRight.Y, bottomRight.Y, bottomLeft.Y);

            return new Box2(left, bottom, right, top);
        }

        /// <inheritdoc />
        public Vector2 WorldToScreen(Vector2 point)
        {
            var newPoint = point;

            CurrentEye.GetViewMatrix(out var viewMatrix);
            newPoint = viewMatrix * newPoint;

            // (inlined version of UiProjMatrix)
            newPoint *= new Vector2(1, -1) * PixelsPerMeter;
            newPoint += _displayManager.ScreenSize / 2f;

            return newPoint;
        }

        /// <inheritdoc />
        public void GetScreenProjectionMatrix(out Matrix3 projMatrix)
        {
            Matrix3 result = default;

            result.R0C0 = PixelsPerMeter;
            result.R1C1 = -PixelsPerMeter;

            var screenSize = _displayManager.ScreenSize;
            result.R0C2 = screenSize.X / 2f;
            result.R1C2 = screenSize.Y / 2f;

            result.R2C2 = 1;

            /* column major
             Sx 0 Tx
             0 Sy Ty
             0  0  1
            */
            projMatrix = result;
        }

        /// <inheritdoc />
        public ScreenCoordinates WorldToScreen(GridCoordinates point)
        {
            var worldCoords = _mapManager.GetGrid(point.GridID).LocalToWorld(point);
            return new ScreenCoordinates(WorldToScreen(worldCoords.Position));
        }

        /// <inheritdoc />
        public GridCoordinates ScreenToWorld(ScreenCoordinates point)
        {
            return ScreenToWorld(point.Position);
        }

        /// <inheritdoc />
        public GridCoordinates ScreenToWorld(Vector2 point)
        {
            var mapCoords = ScreenToMap(point);

            if (!_mapManager.TryFindGridAt(mapCoords, out var grid))
            {
                grid = _mapManager.GetDefaultGrid(mapCoords.MapId);
            }

            return new GridCoordinates(grid.WorldToLocal(mapCoords.Position), grid.Index);
        }

        /// <inheritdoc />
        public MapCoordinates ScreenToMap(Vector2 point)
        {
            var newPoint = point;

            // (inlined version of UiProjMatrix^-1)
            newPoint -= _displayManager.ScreenSize / 2f;
            newPoint *= new Vector2(1, -1) / PixelsPerMeter;

            // view matrix
            CurrentEye.GetViewMatrixInv(out var viewMatrixInv);
            newPoint = viewMatrixInv * newPoint;

            return new MapCoordinates(newPoint, CurrentMap);
        }
    }
}
