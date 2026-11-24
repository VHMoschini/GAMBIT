using System;
using System.Collections.Generic;

namespace Prototipo.Core
{
    /// <summary>
    /// Grade lógica do tabuleiro com topologia determinística e zonas de spawn.
    /// </summary>
    public sealed class BoardGrid
    {
        private readonly BoardTile[,] tiles;
        private readonly List<BoardTile> allTiles;

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<BoardTile> AllTiles => allTiles;

        public BoardGrid(int width, int height, Func<BoardCoord, TileKind> tileKindSelector = null)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            tiles = new BoardTile[Width, Height];
            allTiles = new List<BoardTile>(Width * Height);

            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var coord = new BoardCoord(x, z);
                    TileKind kind = tileKindSelector != null ? tileKindSelector(coord) : TileKind.Normal;
                    var tile = new BoardTile(coord, kind);
                    tiles[x, z] = tile;
                    allTiles.Add(tile);
                }
            }
        }

        public bool HasTile(BoardCoord coord)
        {
            return coord.X >= 0 && coord.X < Width && coord.Z >= 0 && coord.Z < Height;
        }

        public BoardTile GetTile(BoardCoord coord)
        {
            return HasTile(coord) ? tiles[coord.X, coord.Z] : null;
        }

        public bool IsSpawnTileFor(BoardCoord coord, PlayerTeam team)
        {
            BoardTile tile = GetTile(coord);
            if (tile == null)
            {
                return false;
            }

            return team == PlayerTeam.Player1
                ? tile.Kind == TileKind.SpawnP1
                : tile.Kind == TileKind.SpawnP2;
        }

        public IReadOnlyList<BoardCoord> GetSpawnCoords(PlayerTeam team)
        {
            TileKind targetKind = team == PlayerTeam.Player1 ? TileKind.SpawnP1 : TileKind.SpawnP2;
            var coords = new List<BoardCoord>();

            for (int i = 0; i < allTiles.Count; i++)
            {
                if (allTiles[i].Kind == targetKind)
                {
                    coords.Add(allTiles[i].Coord);
                }
            }

            return coords;
        }

        /// <summary>
        /// Cria a arena padrão 7x7 com zonas de spawn nas 2 primeiras/últimas linhas
        /// e obstáculos simétricos na linha central.
        /// </summary>
        public static BoardGrid CreateDefaultArena(int width = 7, int height = 7)
        {
            int safeWidth = Math.Max(3, width);
            int safeHeight = Math.Max(5, height);
            int midZ = safeHeight / 2;

            return new BoardGrid(safeWidth, safeHeight, coord =>
            {
                if (coord.Z == 0 || coord.Z == 1)
                {
                    return TileKind.SpawnP1;
                }

                if (coord.Z == safeHeight - 1 || coord.Z == safeHeight - 2)
                {
                    return TileKind.SpawnP2;
                }

                if (coord.Z == midZ && safeWidth >= 7)
                {
                    if (coord.X == 1 || coord.X == safeWidth - 2)
                    {
                        return TileKind.Hole;
                    }

                    if (coord.X == 2 || coord.X == safeWidth - 3)
                    {
                        return TileKind.Blocked;
                    }
                }

                return TileKind.Normal;
            });
        }
    }
}
