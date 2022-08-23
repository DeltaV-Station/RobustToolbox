using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics
{
    /// <summary>
    ///     Used for doing direct drawing without sprite components, existing GUI controls, etc...
    /// </summary>
    public abstract class DrawingHandleBase : IDisposable
    {
        //private protected IRenderHandle _renderHandle;
        private protected readonly int _handleId;
        public bool Disposed { get; private set; }
        /// <summary>
        ///     Drawing commands that do NOT receive per-vertex modulation get modulated by this.
        ///     Specifically, *DrawPrimitives w/ DrawVertexUV2DColor IS NOT AFFECTED BY THIS*.
        ///     The only code that should ever be setting this is UserInterfaceManager.
        ///     It's absolutely evil statefulness.
        ///     I understand it's existence and operation.
        ///     I understand that removing it would require rewriting all the UI controls everywhere.
        ///     I still wish it a prolonged death - it's a performance nightmare. - 20kdc
        /// </summary>
        public Color Modulate { get; set; } = Color.White;

        public void Dispose()
        {
            Disposed = true;
        }

        public void SetTransform(in Vector2 position, in Angle rotation, in Vector2 scale)
        {
            CheckDisposed();

            var matrix = Matrix3.CreateTransform(in position, in rotation, in scale);
            SetTransform(in matrix);
        }

        public void SetTransform(in Vector2 position, in Angle rotation)
        {
            var matrix = Matrix3.CreateTransform(in position, in rotation);
            SetTransform(in matrix);
        }

        public abstract void SetTransform(in Matrix3 matrix);

        public abstract void UseShader(ShaderInstance? shader);

        // ---- DrawPrimitives: Vector2 API ----

        /// <summary>
        ///     Draws arbitrary geometry primitives with a flat color.
        /// </summary>
        /// <param name="primitiveTopology">The topology of the primitives to draw.</param>
        /// <param name="vertices">The set of vertices to render.</param>
        /// <param name="color">The color to draw with.</param>
        public void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, ReadOnlySpan<Vector2> vertices,
            Color color)
        {
            var realColor = color * Modulate;

            // TODO: Maybe don't stackalloc if the data is too large.
            Span<DrawVertexUV2DColor> drawVertices = stackalloc DrawVertexUV2DColor[vertices.Length];
            PadVerticesV2(vertices, drawVertices, realColor);

            DrawPrimitives(primitiveTopology, Texture.White, drawVertices);
        }

        /// <summary>
        ///     Draws arbitrary indexed geometry primitives with a flat color.
        /// </summary>
        /// <param name="primitiveTopology">The topology of the primitives to draw.</param>
        /// <param name="indices">The indices into <paramref name="vertices"/> to render.</param>
        /// <param name="vertices">The set of vertices to render.</param>
        /// <param name="color">The color to draw with.</param>
        public void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, ReadOnlySpan<ushort> indices,
            ReadOnlySpan<Vector2> vertices, Color color)
        {
            var realColor = color * Modulate;

            // TODO: Maybe don't stackalloc if the data is too large.
            Span<DrawVertexUV2DColor> drawVertices = stackalloc DrawVertexUV2DColor[vertices.Length];
            PadVerticesV2(vertices, drawVertices, realColor);

            DrawPrimitives(primitiveTopology, Texture.White, indices, drawVertices);
        }

        private void PadVerticesV2(ReadOnlySpan<Vector2> input, Span<DrawVertexUV2DColor> output, Color color)
        {
            Color colorLinear = Color.FromSrgb(color);
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = new DrawVertexUV2DColor(input[i], (0.5f, 0.5f), colorLinear);
            }
        }

        // ---- DrawPrimitives: DrawVertexUV2D API ----

        /// <summary>
        ///     Draws arbitrary geometry primitives with a texture.
        /// </summary>
        /// <param name="primitiveTopology">The topology of the primitives to draw.</param>
        /// <param name="texture">The texture to render with.</param>
        /// <param name="vertices">The set of vertices to render.</param>
        /// <param name="color">The color to draw with.</param>
        public void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture, ReadOnlySpan<DrawVertexUV2D> vertices,
            Color? color = null)
        {
            var realColor = (color ?? Color.White) * Modulate;

            // TODO: Maybe don't stackalloc if the data is too large.
            Span<DrawVertexUV2DColor> drawVertices = stackalloc DrawVertexUV2DColor[vertices.Length];
            PadVerticesUV(vertices, drawVertices, realColor);

            DrawPrimitives(primitiveTopology, texture, drawVertices);
        }

        /// <summary>
        ///     Draws arbitrary geometry primitives with a texture.
        /// </summary>
        /// <param name="primitiveTopology">The topology of the primitives to draw.</param>
        /// <param name="texture">The texture to render with.</param>
        /// <param name="vertices">The set of vertices to render.</param>
        /// <param name="indices">The indices into <paramref name="vertices"/> to render.</param>
        /// <param name="color">The color to draw with.</param>
        public void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture, ReadOnlySpan<ushort> indices,
            ReadOnlySpan<DrawVertexUV2D> vertices, Color? color = null)
        {
            var realColor = (color ?? Color.White) * Modulate;

            // TODO: Maybe don't stackalloc if the data is too large.
            Span<DrawVertexUV2DColor> drawVertices = stackalloc DrawVertexUV2DColor[vertices.Length];
            PadVerticesUV(vertices, drawVertices, realColor);

            DrawPrimitives(primitiveTopology, texture, indices, drawVertices);
        }

        private void PadVerticesUV(ReadOnlySpan<DrawVertexUV2D> input, Span<DrawVertexUV2DColor> output, Color color)
        {
            Color colorLinear = Color.FromSrgb(color);
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = new DrawVertexUV2DColor(input[i], colorLinear);
            }
        }

        // ---- End wrappers ----

        /// <summary>
        ///     Draws arbitrary geometry primitives with a texture.
        ///     Be aware that this ignores the Modulate property! Apply it yourself if necessary.
        /// </summary>
        /// <param name="primitiveTopology">The topology of the primitives to draw.</param>
        /// <param name="texture">The texture to render with.</param>
        /// <param name="vertices">The set of vertices to render.</param>
        public abstract void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
            ReadOnlySpan<DrawVertexUV2DColor> vertices);

        /// <summary>
        ///     Draws arbitrary geometry primitives with a flat color.
        ///     Be aware that this ignores the Modulate property! Apply it yourself if necessary.
        /// </summary>
        /// <param name="primitiveTopology">The topology of the primitives to draw.</param>
        /// <param name="texture">The texture to render with.</param>
        /// <param name="indices">The indices into <paramref name="vertices"/> to render.</param>
        /// <param name="vertices">The set of vertices to render.</param>
        public abstract void DrawPrimitives(DrawPrimitiveTopology primitiveTopology, Texture texture,
            ReadOnlySpan<ushort> indices,
            ReadOnlySpan<DrawVertexUV2DColor> vertices);

        [DebuggerStepThrough]
        protected void CheckDisposed()
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(DrawingHandleBase));
            }
        }

        public abstract void DrawCircle(Vector2 position, float radius, Color color, bool filled = true);

        public abstract void DrawLine(Vector2 from, Vector2 to, Color color);

        public abstract void RenderInRenderTarget(IRenderTarget target, Action a, Color clearColor=default);

        /// <summary>
        ///     Draw a simple string to the screen at the specified position.
        /// </summary>
        /// <remarks>
        ///     This method is primarily intended for debug purposes and does not handle things like UI scaling.
        /// </remarks>
        /// <returns>
        ///     The space taken up (horizontal and vertical) by the text.
        /// </returns>
        /// <param name="font">The font to render with.</param>
        /// <param name="pos">The top-left corner to start drawing text at.</param>
        /// <param name="str">The text to draw.</param>
        /// <param name="color">The color of text to draw.</param>
        public Vector2 DrawString(Font font, Vector2 pos, string str, Color color)
            => DrawString(font, pos, str, 1, color);

        /// <summary>
        ///     Draw a simple string to the screen at the specified position.
        /// </summary>
        /// <remarks>
        ///     This method is primarily intended for debug purposes and does not handle things like UI scaling.
        /// </remarks>
        /// <returns>
        ///     The space taken up (horizontal and vertical) by the text.
        /// </returns>
        /// <param name="font">The font to render with.</param>
        /// <param name="pos">The top-left corner to start drawing text at.</param>
        /// <param name="str">The text to draw.</param>
        public Vector2 DrawString(Font font, Vector2 pos, string str)
            => DrawString(font, pos, str, Color.White);

        public Vector2 DrawString(Font font, Vector2 pos, ReadOnlySpan<char> str, float scale, Color color)
        {
            // TODO: Take in vertical and horizontal alignment.
            var advanceTotal = Vector2.Zero;
            var baseLine = new Vector2(pos.X, font.GetAscent(scale) + pos.Y);
            var lineHeight = font.GetLineHeight(scale);

            foreach (var rune in str.EnumerateRunes())
            {
                if (rune == new Rune('\n'))
                {
                    baseLine.X = pos.X;
                    baseLine.Y += lineHeight;
                    advanceTotal.Y += lineHeight;
                    continue;
                }

                var advance = font.DrawChar(this, rune, baseLine, scale, color);
                advanceTotal.X += advance;
                baseLine += new Vector2(advance, 0);
            }

            return advanceTotal;
        }
    }

    /// <summary>
    ///     2D Vertex that contains both position and UV coordinates.
    /// </summary>
    public struct DrawVertexUV2D
    {
        public Vector2 Position;
        public Vector2 UV;

        public DrawVertexUV2D(Vector2 position, Vector2 uv)
        {
            Position = position;
            UV = uv;
        }
    }

    /// <summary>
    ///     2D Vertex that contains position and UV coordinates, and a modulation colour (Linear!!!)
    ///     NOTE: This is directly cast into Clyde Vertex2D!!!!
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DrawVertexUV2DColor
    {
        public Vector2 Position;
        public Vector2 UV;
        /// <summary>
        ///     Modulation colour for this vertex.
        ///     Note that this color is in linear space.
        /// </summary>
        public Color Color;

        /// <param name="position">The location.</param>
        /// <param name="uv">The texture coordinate.</param>
        /// <param name="col">Modulation colour (In linear space, use Color.FromSrgb if needed)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DrawVertexUV2DColor(Vector2 position, Vector2 uv, Color col)
        {
            Position = position;
            UV = uv;
            Color = col;
        }

        /// <param name="position">The location.</param>
        /// <param name="col">Modulation colour (In linear space, use Color.FromSrgb if needed)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DrawVertexUV2DColor(Vector2 position, Color col)
        {
            Position = position;
            UV = new Vector2(0.5f, 0.5f);
            Color = col;
        }

        /// <param name="b">The existing position/UV pair.</param>
        /// <param name="col">Modulation colour (In linear space, use Color.FromSrgb if needed)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DrawVertexUV2DColor(DrawVertexUV2D b, Color col)
        {
            Position = b.Position;
            UV = b.UV;
            Color = col;
        }
    }
}
