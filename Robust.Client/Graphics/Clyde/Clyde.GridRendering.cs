using System;
using System.Collections.Generic;
using OpenToolkit.Graphics.OpenGL4;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Client.Graphics.Clyde
{
    internal partial class Clyde
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private readonly Dictionary<EntityUid, Dictionary<Vector2i, MapChunkData>> _mapChunkData =
            new();

        private int _verticesPerChunk(MapChunk chunk) => chunk.ChunkSize * chunk.ChunkSize * 4;
        private int _indicesPerChunk(MapChunk chunk) => chunk.ChunkSize * chunk.ChunkSize * GetQuadBatchIndexCount();

        private void _drawGrids(Viewport viewport, Box2Rotated worldBounds, IEye eye)
        {
            var mapId = eye.Position.MapId;
            if (!_mapManager.MapExists(mapId))
            {
                // fall back to nullspace map
                mapId = MapId.Nullspace;
            }

            SetTexture(TextureUnit.Texture0, _tileDefinitionManager.TileTextureAtlas);
            SetTexture(TextureUnit.Texture1, _lightingReady ? viewport.LightRenderTarget.Texture : _stockTextureWhite);

            var gridProgram = ActivateShaderInstance(_defaultShader.Handle).Item1;
            SetupGlobalUniformsImmediate(gridProgram, (ClydeTexture) _tileDefinitionManager.TileTextureAtlas);

            gridProgram.SetUniformTextureMaybe(UniIMainTexture, TextureUnit.Texture0);
            gridProgram.SetUniformTextureMaybe(UniILightTexture, TextureUnit.Texture1);
            gridProgram.SetUniform(UniIModUV, new Vector4(0, 0, 1, 1));

            var grids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(mapId, worldBounds, ref grids);
            foreach (var mapGrid in grids)
            {
                if (!_mapChunkData.ContainsKey(mapGrid))
                    continue;

                var transform = _entityManager.GetComponent<TransformComponent>(mapGrid);
                gridProgram.SetUniform(UniIModelMatrix, transform.WorldMatrix);
                var enumerator = mapGrid.Comp.GetMapChunks(worldBounds);
                var data = _mapChunkData[mapGrid];

                while (enumerator.MoveNext(out var chunk))
                {
                    DebugTools.Assert(chunk.FilledTiles > 0);
                    if (!data.TryGetValue(chunk.Indices, out MapChunkData? datum))
                        data[chunk.Indices] = datum = _initChunkBuffers(mapGrid, chunk);

                    if (datum.Dirty)
                        _updateChunkMesh(mapGrid, chunk, datum);

                    DebugTools.Assert(datum.TileCount > 0);
                    if (datum.TileCount == 0)
                        continue;

                    BindVertexArray(datum.VAO);
                    CheckGlError();

                    _debugStats.LastGLDrawCalls += 1;
                    GL.DrawElements(GetQuadGLPrimitiveType(), datum.TileCount * GetQuadBatchIndexCount(), DrawElementsType.UnsignedShort, 0);
                    CheckGlError();
                }
            }
        }

        private void _updateChunkMesh(Entity<MapGridComponent> grid, MapChunk chunk, MapChunkData datum)
        {
            Span<ushort> indexBuffer = stackalloc ushort[_indicesPerChunk(chunk)];
            Span<Vertex2D> vertexBuffer = stackalloc Vertex2D[_verticesPerChunk(chunk)];

            var i = 0;
            var cSz = grid.Comp.ChunkSize;
            var cScaled = chunk.Indices * cSz;
            for (ushort x = 0; x < cSz; x++)
            {
                for (ushort y = 0; y < cSz; y++)
                {
                    var tile = chunk.GetTile(x, y);
                    if (tile.IsEmpty)
                        continue;

                    var regionMaybe = _tileDefinitionManager.TileAtlasRegion(tile);

                    Box2 region;
                    if (regionMaybe == null || regionMaybe.Length <= tile.Variant)
                    {
                        region = _tileDefinitionManager.ErrorTileRegion;
                    }
                    else
                    {
                        region = regionMaybe[tile.Variant];
                    }

                    var gx = x + cScaled.X;
                    var gy = y + cScaled.Y;

                    var vIdx = i * 4;
                    vertexBuffer[vIdx + 0] = new Vertex2D(gx, gy, region.Left, region.Bottom, Color.White);
                    vertexBuffer[vIdx + 1] = new Vertex2D(gx + 1, gy, region.Right, region.Bottom, Color.White);
                    vertexBuffer[vIdx + 2] = new Vertex2D(gx + 1, gy + 1, region.Right, region.Top, Color.White);
                    vertexBuffer[vIdx + 3] = new Vertex2D(gx, gy + 1, region.Left, region.Top, Color.White);
                    var nIdx = i * GetQuadBatchIndexCount();
                    var tIdx = (ushort)(i * 4);
                    QuadBatchIndexWrite(indexBuffer, ref nIdx, tIdx);
                    i += 1;
                }
            }

            GL.BindVertexArray(datum.VAO);
            CheckGlError();
            datum.EBO.Use();
            datum.VBO.Use();
            datum.EBO.Reallocate(indexBuffer[..(i * GetQuadBatchIndexCount())]);
            datum.VBO.Reallocate(vertexBuffer[..(i * 4)]);
            datum.Dirty = false;
            datum.TileCount = i;
        }

        private unsafe MapChunkData _initChunkBuffers(Entity<MapGridComponent> grid, MapChunk chunk)
        {
            var vao = GenVertexArray();
            BindVertexArray(vao);
            CheckGlError();

            var vboSize = _verticesPerChunk(chunk) * sizeof(Vertex2D);
            var eboSize = _indicesPerChunk(chunk) * sizeof(ushort);

            var vbo = new GLBuffer(this, BufferTarget.ArrayBuffer, BufferUsageHint.DynamicDraw,
                vboSize, $"Grid {grid.Owner} chunk {chunk.Indices} VBO");
            var ebo = new GLBuffer(this, BufferTarget.ElementArrayBuffer, BufferUsageHint.DynamicDraw,
                eboSize, $"Grid {grid.Owner} chunk {chunk.Indices} EBO");

            ObjectLabelMaybe(ObjectLabelIdentifier.VertexArray, vao, $"Grid {grid.Owner} chunk {chunk.Indices} VAO");
            SetupVAOLayout();
            CheckGlError();

            // Assign VBO and EBO to VAO.
            // OpenGL 3.x is such a good API.
            vbo.Use();
            ebo.Use();

            var datum = new MapChunkData(vao, vbo, ebo)
            {
                Dirty = true
            };

            return datum;
        }

        private void _updateOnGridModified(GridModifiedEvent args)
        {
            var gridData = _mapChunkData.GetOrNew(args.GridEnt);

            foreach (var (pos, _) in args.Modified)
            {
                var chunk = args.Grid.GridTileToChunkIndices(pos);
                if (gridData.TryGetValue(chunk, out var data))
                    data.Dirty = true;
            }

            foreach (var chunk in args.RemovedChunks)
            {
                if (gridData.Remove(chunk, out var data))
                    DeleteChunk(data);
            }
        }

        private void DeleteChunk(MapChunkData data)
        {
            DeleteVertexArray(data.VAO);
            CheckGlError();
            data.VBO.Delete();
            data.EBO.Delete();
        }

        private void _updateTileMapOnUpdate(ref TileChangedEvent args)
        {
            var guid = args.NewTile.GridUid;
            var data = _mapChunkData.GetOrNew(guid);
            var chunk = _mapManager.GetGrid(guid).GridTileToChunkIndices(args.NewTile.GridIndices);
            if (data.TryGetValue(chunk, out var datum))
                datum.Dirty = true;
        }

        private void _updateOnGridCreated(GridStartupEvent ev)
        {
            var gridId = ev.EntityUid;
            _mapChunkData.GetOrNew(gridId);
        }

        private void _updateOnGridRemoved(GridRemovalEvent ev)
        {
            var gridId = ev.EntityUid;

            var data = _mapChunkData[gridId];
            foreach (var chunkDatum in data.Values)
            {
                DeleteChunk(chunkDatum);
            }

            _mapChunkData.Remove(gridId);
        }

        private sealed class MapChunkData
        {
            public bool Dirty;
            public readonly uint VAO;
            public readonly GLBuffer VBO;
            public readonly GLBuffer EBO;
            public int TileCount;

            public MapChunkData(uint vao, GLBuffer vbo, GLBuffer ebo)
            {
                VAO = vao;
                VBO = vbo;
                EBO = ebo;
            }
        }
    }
}
