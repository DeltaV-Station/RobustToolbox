﻿using SFML.Graphics;
using SFML.System;
using SS14.Client.Graphics.Render;
using SS14.Client.Graphics.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BlendMode = SS14.Client.Graphics.Render.BlendMode;
using RenderStates = SS14.Client.Graphics.Render.RenderStates;
using SBlendMode = SFML.Graphics.BlendMode;
using SRenderStates = SFML.Graphics.RenderStates;
using Texture = SS14.Client.Graphics.Textures.Texture;

namespace SS14.Client.Graphics.Sprites
{
    /// <summary>
    /// Provides optimized drawing of sprites
    /// </summary>
    [DebuggerDisplay("[SpriteBatch] IsDrawing: {Drawing} | ")]
    public class SpriteBatch : IDrawable, Drawable
    {
        private QueueItem activeItem;
        private List<QueueItem> QueuedTextures = new List<QueueItem>();
        private Queue<QueueItem> RecycleQueue = new Queue<QueueItem>();
        private readonly uint Max;
        private int count;
        private bool Drawing;

        public int Count
        {
            get { return count; }
        }

        public BlendMode BlendingSettings;

        public SpriteBatch(uint maxCapacity = 100000)
        {
            Max = maxCapacity * 4;
            BlendingSettings = new BlendMode(BlendMode.Factor.SrcAlpha, BlendMode.Factor.OneMinusDstAlpha, BlendMode.Equation.Add, BlendMode.Factor.SrcAlpha, BlendMode.Factor.OneMinusSrcAlpha, BlendMode.Equation.Add);
        }

        public void BeginDrawing()
        {
            count = 0;
            // we use these a lot, and the overall number of textures
            // remains stable, so recycle them to avoid excess calls into
            // the native constructor.
            foreach (var Entry in QueuedTextures)
            {
                Entry.Verticies.Clear();
                Entry.Texture = null;
                RecycleQueue.Enqueue(Entry);
            }
            QueuedTextures.Clear();
            Drawing = true;
            activeItem = null;
        }

        public void EndDrawing()
        {
            Drawing = false;
        }

        private void Using(Texture texture)
        {
            if (!Drawing)
                throw new Exception("Call Begin first.");

            if (activeItem == null || activeItem.Texture != texture)
            {
                if (RecycleQueue.Count > 0)
                {
                    activeItem = RecycleQueue.Dequeue();
                    activeItem.Texture = texture;
                }
                else
                {
                    activeItem = new QueueItem(texture);
                }
                QueuedTextures.Add(activeItem);
            }
        }

        public void Draw(IEnumerable<Sprite> sprites)
        {
            foreach (var s in sprites)
            {
                Draw(s);
            }
        }

        public void Draw(Sprite S)
        {
            count++;
            Using(S.Texture);
            Vector2f Scale = new Vector2f(S.Scale.X, S.Scale.Y);
            float sin = 0, cos = 1;

            S.Rotation = S.Rotation / 180 * (float)Math.PI;
            sin = (float)Math.Sin(S.Rotation);
            cos = (float)Math.Cos(S.Rotation);

            var pX = -S.Origin.X * S.Scale.X;
            var pY = -S.Origin.Y * S.Scale.Y;
            Scale.X *= S.TextureRect.Width;
            Scale.Y *= S.TextureRect.Height;

            activeItem.Verticies.Append
                (
                 new Vertex(
                        new Vector2f(
                            pX * cos - pY * sin + S.Position.X,
                            pX * sin + pY * cos + S.Position.Y),
                            S.Color.Convert(),
                        new Vector2f(
                            S.TextureRect.Left,
                            S.TextureRect.Top)
                            )
               );

            pX += Scale.X;
            activeItem.Verticies.Append
                (
                new Vertex(
                        new Vector2f(
                            pX * cos - pY * sin + S.Position.X,
                            pX * sin + pY * cos + S.Position.Y),
                            S.Color.Convert(),
                        new Vector2f(
                            S.TextureRect.Left + S.TextureRect.Width,
                            S.TextureRect.Top)
                          )
                );

            pY += Scale.Y;
            activeItem.Verticies.Append
                (
                new Vertex(
                        new Vector2f(
                            pX * cos - pY * sin + S.Position.X,
                            pX * sin + pY * cos + S.Position.Y),
                            S.Color.Convert(),
                        new Vector2f(
                            S.TextureRect.Left + S.TextureRect.Width,
                            S.TextureRect.Top + S.TextureRect.Height)
                         )
                );

            pX -= Scale.X;

            activeItem.Verticies.Append(
                new Vertex(
                        new Vector2f(
                            pX * cos - pY * sin + S.Position.X,
                            pX * sin + pY * cos + S.Position.Y),
                            S.Color.Convert(),
                        new Vector2f(
                            S.TextureRect.Left,
                            S.TextureRect.Top + S.TextureRect.Height)
                        )
                );
        }

        public void Draw(RenderTarget target, SRenderStates Renderstates)
        {
            if (Drawing)
            {
                throw new InvalidOperationException("Call End first.");
            }

            foreach (var item in QueuedTextures)
            {
                Renderstates.Texture = item.Texture.SFMLTexture;
                Renderstates.BlendMode = (SBlendMode)BlendingSettings;

                item.Verticies.Draw(target, Renderstates);
            }
        }

        public void Draw()
        {
            Draw(CluwneLib.CurrentRenderTarget, CluwneLib.ShaderRenderState);
        }

        public void Draw(IRenderTarget target, RenderStates renderStates)
        {
            Draw(target.SFMLTarget, renderStates.SFMLRenderStates);
        }

        Drawable IDrawable.SFMLDrawable => this;

        public void Dispose()
        {
        }

        [DebuggerDisplay("[QueueItem] Name: {ID} | Texture: {Texture} | Verticies: {Verticies}")]
        private class QueueItem
        {
            public Texture Texture;
            public VertexArray Verticies;

            public QueueItem(Texture Tex)
            {
                Texture = Tex;
                Verticies = new VertexArray(PrimitiveType.Quads);
            }
        }
    }
}
