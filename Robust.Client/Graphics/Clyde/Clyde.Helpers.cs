using OpenToolkit.Graphics.OpenGL4;
using Robust.Shared.Maths;
using System.Runtime.CompilerServices;
using System;
using System.Buffers;

namespace Robust.Client.Graphics.Clyde
{
    internal sealed partial class Clyde
    {
        private void GLClearColor(Color color)
        {
            GL.ClearColor(color.R, color.G, color.B, color.A);
        }

        private void SetTexture(TextureUnit unit, Texture texture)
        {
            var ct = (ClydeTexture) texture;
            SetTexture(unit, ct.TextureId);
        }

        private void SetTexture(TextureUnit unit, ClydeHandle textureId)
        {
            var glHandle = _loadedTextures[textureId].OpenGLObject;
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, glHandle.Handle);
        }

        private static long EstPixelSize(PixelInternalFormat format)
        {
            return format switch
            {
                PixelInternalFormat.Rgba8 => 4,
                PixelInternalFormat.Rgba16f => 8,
                PixelInternalFormat.Srgb8Alpha8 => 4,
                PixelInternalFormat.R11fG11fB10f => 4,
                PixelInternalFormat.R32f => 4,
                PixelInternalFormat.Rg32f => 4,
                PixelInternalFormat.R8 => 1,
                _ => 0
            };
        }

        // Sets up uniforms (temporary, this should be moved to GLShaderProgram.Use)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TempSetupUniformBlockEmulator(GLShaderProgram program)
        {
            ProjViewUBO.Apply(program);
            UniformConstantsUBO.Apply(program);
            program.SetUniformMaybe("TEXTURE_SRGB", 0.0f);
            program.SetUniformMaybe("FRAMEBUFFER_SRGB", 0.0f);
        }

        // Gets the primitive type required by QuadBatchIndexWrite.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BatchPrimitiveType GetQuadBatchPrimitiveType()
        {
            return _hasGLPrimitiveRestart ? BatchPrimitiveType.TriangleFan : BatchPrimitiveType.TriangleList;
        }

        // Gets the PrimitiveType version of GetQuadBatchPrimitiveType
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PrimitiveType GetQuadGLPrimitiveType()
        {
            return _hasGLPrimitiveRestart ? PrimitiveType.TriangleFan : PrimitiveType.Triangles;
        }

        // Gets the amount of indices required by QuadBatchIndexWrite.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetQuadBatchIndexCount()
        {
            // PR: Need 5 indices per quad: 4 to draw the quad with triangle strips and another one as primitive restart.
            // no PR: Need 6 indices per quad: 2 triangles
            return _hasGLPrimitiveRestart ? 5 : 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void QuadBatchIndexWrite(Span<ushort> indexData, ref int nIdx, ushort tIdx)
        {
            QuadBatchIndexWrite(indexData, ref nIdx, tIdx, (ushort) (tIdx + 1), (ushort) (tIdx + 2), (ushort) (tIdx + 3));
        }

        // Writes a quad into the index buffer. Note that the 'middle line' is from tIdx0 to tIdx2.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void QuadBatchIndexWrite(Span<ushort> indexData, ref int nIdx, ushort tIdx0, ushort tIdx1, ushort tIdx2, ushort tIdx3)
        {
            if (_hasGLPrimitiveRestart)
            {
                // PJB's fancy triangle fan isolated to a quad with primitive restart
                indexData[nIdx + 0] = tIdx0;
                indexData[nIdx + 1] = tIdx1;
                indexData[nIdx + 2] = tIdx2;
                indexData[nIdx + 3] = tIdx3;
                indexData[nIdx + 4] = PrimitiveRestartIndex;
                nIdx += 5;
            }
            else
            {
                // 20kdc's boring two separate triangles
                indexData[nIdx + 0] = tIdx0;
                indexData[nIdx + 1] = tIdx1;
                indexData[nIdx + 2] = tIdx2;
                indexData[nIdx + 3] = tIdx0;
                indexData[nIdx + 4] = tIdx2;
                indexData[nIdx + 5] = tIdx3;
                nIdx += 6;
            }
        }
    }
}
