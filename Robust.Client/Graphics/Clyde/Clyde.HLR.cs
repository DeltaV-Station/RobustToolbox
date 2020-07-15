﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Client.Graphics.ClientEye;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Interfaces.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Client.Graphics.Clyde
{
    // "HLR" stands for "high level rendering" here.
    // The left side of my monitor only has so much space, OK?
    // The idea is this shouldn't contain too much GL specific stuff.
    internal partial class Clyde
    {
        public ClydeDebugLayers DebugLayers { get; set; }

        private readonly RefList<(SpriteComponent sprite, Matrix3 worldMatrix, Angle worldRotation, float yWorldPos)> _drawingSpriteList
            =
            new RefList<(SpriteComponent, Matrix3, Angle, float)>();

        public void Render()
        {
            CheckTransferringScreenshots();

            var size = ScreenSize;
            if (size.X == 0 || size.Y == 0 || _isMinimized)
            {
                ClearFramebuffer(Color.Black);

                // We have to keep running swapbuffers here
                // or else the user's PC will turn into a heater!!
                SwapBuffers();
                return;
            }

            _debugStats.Reset();

            // Basic pre-render busywork.
            // Clear screen to black.
            ClearFramebuffer(Color.Black);

            // Update shared UBOs.
            _updateUniformConstants();

            SetSpaceFull(CurrentSpace.ScreenSpace);

            // Short path to render only the splash.
            if (_drawingSplash)
            {
                DrawSplash(_renderHandle);
                FlushRenderQueue();
                SwapBuffers();
                return;
            }

            RenderOverlays(OverlaySpace.ScreenSpaceBelowWorld);

            SetSpaceFull(CurrentSpace.WorldSpace);

            RenderViewport(_mainViewport);

            SetSpaceFull(CurrentSpace.ScreenSpace);

            // TODO: Re-enable this
            /*
            if (DebugLayers == ClydeDebugLayers.Fov)
            {
                // I'm refactoring this code and I found this comment:
                // NOTE
                // Yes, it just says "NOTE". Thank you past me.
                // Anyways I'm 99% sure this was about the fact that this debug layer is actually broken.
                // Because the math is wrong.
                // So there are distortions from incorrect projection.
                _renderHandle.UseShader(_fovDebugShaderInstance);
                _renderHandle.DrawingHandleScreen.SetTransform(Matrix3.Identity);
                var pos = UIBox2.FromDimensions(ScreenSize / 2 - (200, 200), (400, 400));
                _renderHandle.DrawingHandleScreen.DrawTextureRect(FovTexture, pos);
            }

            if (DebugLayers == ClydeDebugLayers.Light)
            {
                _renderHandle.UseShader(null);
                _renderHandle.DrawingHandleScreen.SetTransform(Matrix3.Identity);
                _renderHandle.DrawingHandleScreen.DrawTextureRect(_wallBleedIntermediateRenderTarget2.Texture, UIBox2.FromDimensions(Vector2.Zero, ScreenSize), new Color(1, 1, 1, 0.5f));
            }
            */

            TakeScreenshot(ScreenshotType.BeforeUI);

            RenderOverlays(OverlaySpace.ScreenSpace);

            using (DebugGroup("UI"))
            {
                _userInterfaceManager.Render(_renderHandle);
                FlushRenderQueue();
            }

            TakeScreenshot(ScreenshotType.AfterUI);

            // And finally, swap those buffers!
            SwapBuffers();
        }

        private void RenderOverlays(OverlaySpace space)
        {
            using (DebugGroup($"Overlays: {space}"))
            {
                var list = new List<Overlay>();

                foreach (var overlay in _overlayManager.AllOverlays)
                {
                    if (overlay.Space == space)
                    {
                        list.Add(overlay);
                    }
                }

                list.Sort(OverlayComparer.Instance);

                foreach (var overlay in list)
                {
                    overlay.ClydeRender(_renderHandle);
                }

                FlushRenderQueue();
            }
        }

        private void DrawEntities(Viewport viewport, Box2 worldBounds)
        {
            if (_eyeManager.CurrentMap == MapId.Nullspace || !_mapManager.HasMapEntity(_eyeManager.CurrentMap))
            {
                return;
            }

            var screenSize = viewport.Size;

            // So we could calculate the correct size of the entities based on the contents of their sprite...
            // Or we can just assume that no entity is larger than 10x10 and get a stupid easy check.
            // TODO: Make this check more accurate.
            var widerBounds = worldBounds.Enlarged(5);

            ProcessSpriteEntities(_eyeManager.CurrentMap, widerBounds, _drawingSpriteList);

            // We use a separate list for indexing so that the sort is faster.
            var indexList = ArrayPool<int>.Shared.Rent(_drawingSpriteList.Count);

            for (var i = 0; i < _drawingSpriteList.Count; i++)
            {
                indexList[i] = i;
            }

            Array.Sort(indexList, 0, _drawingSpriteList.Count, new SpriteDrawingOrderComparer(_drawingSpriteList));

            for (var i = 0; i < _drawingSpriteList.Count; i++)
            {
                ref var entry = ref _drawingSpriteList[indexList[i]];
                Vector2i roundedPos = default;
                if (entry.sprite.PostShader != null)
                {
                    _renderHandle.UseRenderTarget(EntityPostRenderTarget);
                    _renderHandle.Clear(new Color());
                    // Calculate viewport so that the entity thinks it's drawing to the same position,
                    // which is necessary for light application,
                    // but it's ACTUALLY drawing into the center of the render target.
                    var spritePos = entry.sprite.Owner.Transform.WorldPosition;
                    var screenPos = _eyeManager.WorldToScreen(spritePos);
                    var (roundedX, roundedY) = roundedPos = (Vector2i) screenPos;
                    var flippedPos = new Vector2i(roundedX, screenSize.Y - roundedY);
                    flippedPos -= EntityPostRenderTarget.Size / 2;
                    _renderHandle.Viewport(Box2i.FromDimensions(-flippedPos, screenSize));
                }

                entry.sprite.Render(_renderHandle.DrawingHandleWorld, entry.worldMatrix, entry.worldRotation);

                if (entry.sprite.PostShader != null)
                {
                    _renderHandle.UseRenderTarget(null);
                    _renderHandle.Viewport(Box2i.FromDimensions(Vector2i.Zero, screenSize));

                    _renderHandle.UseShader(entry.sprite.PostShader);
                    _renderHandle.SetSpace(CurrentSpace.ScreenSpace);
                    _renderHandle.SetModelTransform(Matrix3.Identity);

                    var rounded = roundedPos - EntityPostRenderTarget.Size / 2;

                    var box = Box2i.FromDimensions(rounded, EntityPostRenderTarget.Size);

                    _renderHandle.DrawTextureScreen(EntityPostRenderTarget.Texture,
                        box.BottomLeft, box.BottomRight, box.TopLeft, box.TopRight,
                        Color.White, null);

                    _renderHandle.SetSpace(CurrentSpace.WorldSpace);
                    _renderHandle.UseShader(null);
                }
            }

            _drawingSpriteList.Clear();

            FlushRenderQueue();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ProcessSpriteEntities(MapId map, Box2 worldBounds, RefList<(SpriteComponent sprite, Matrix3 matrix, Angle worldRot, float yWorldPos)> list)
        {
            var spriteSystem = _entitySystemManager.GetEntitySystem<RenderingTreeSystem>();

            var tree = spriteSystem.GetSpriteTreeForMap(map);

            var sprites = tree.Query(worldBounds, true);

            foreach (var sprite in sprites)
            {
                if (sprite.ContainerOccluded || !sprite.Visible)
                {
                    continue;
                }

                var entity = sprite.Owner;
                var transform = entity.Transform;

                ref var entry = ref list.AllocAdd();
                entry.sprite = sprite;
                entry.worldRot = transform.WorldRotation;
                entry.matrix = transform.WorldMatrix;
                var worldPos = entry.matrix.Transform(transform.LocalPosition);
                entry.yWorldPos = worldPos.Y;
            }
        }

        private void DrawSplash(IRenderHandle handle)
        {
            var texture = _resourceCache.GetResource<TextureResource>("/Textures/Logo/logo.png").Texture;

            handle.DrawingHandleScreen.DrawTexture(texture, (ScreenSize - texture.Size) / 2);
        }

        private void RenderViewport(Viewport viewport)
        {
            var oldVp = _currentViewport;
            _currentViewport = viewport;

            // Calculate world-space AABB for camera, to cull off-screen things.
            var eye = _eyeManager.CurrentEye;
            var worldBounds = Box2.CenteredAround(eye.Position.Position,
                _framebufferSize / (float) EyeManager.PixelsPerMeter * eye.Zoom);

            if (_eyeManager.CurrentMap != MapId.Nullspace)
            {
                using (DebugGroup("Lights"))
                {
                    DrawLightsAndFov(viewport, worldBounds, eye);
                }

                using (DebugGroup("Grids"))
                {
                    _drawGrids(worldBounds);
                }

                using (DebugGroup("Entities"))
                {
                    DrawEntities(viewport, worldBounds);
                }

                RenderOverlays(OverlaySpace.WorldSpace);

                if (_lightManager.Enabled && eye.DrawFov)
                {
                    ApplyFovToBuffer(viewport, eye);
                }
            }

            _lightingReady = false;

            _currentViewport = oldVp;
        }

        private sealed class OverlayComparer : IComparer<Overlay>
        {
            public static readonly OverlayComparer Instance = new OverlayComparer();

            public int Compare(Overlay? x, Overlay? y)
            {
                var zX = x?.ZIndex ?? 0;
                var zY = y?.ZIndex ?? 0;
                return zX.CompareTo(zY);
            }
        }
    }
}
