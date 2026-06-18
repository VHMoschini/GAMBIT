using System;
using System.Collections.Generic;

namespace Game.Core.Grid
{
    /// <summary>
    /// Grid lógico esparso (POCO, testável em edit mode). Mapeia <see cref="GridCoord"/> → <see cref="Tile"/>
    /// num dicionário: <b>buraco = ausência de chave</b>.
    ///
    /// O grid só responde topologia ("essa célula existe?", "quais são meus vizinhos?") e ocupação. A
    /// <i>política</i> de movimento (padrões fixos por peça, deslizar com caminho livre) vive fora do grid,
    /// na validação do comando de movimento (ver arquitetura seção 6). Um MonoBehaviour fino
    /// (<c>GridRuntime</c>) carrega os dados baked e expõe esta instância.
    /// </summary>
    public sealed class GridSystem
    {
        private readonly Dictionary<GridCoord, Tile> _tiles;

        public GridSystem(IEqualityComparer<GridCoord> comparer = null)
        {
            _tiles = new Dictionary<GridCoord, Tile>(comparer);
        }

        public GridSystem(IEnumerable<Tile> tiles, IEqualityComparer<GridCoord> comparer = null)
            : this(comparer)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            foreach (var tile in tiles)
                AddTile(tile);
        }

        /// <summary>Quantas células existem (buracos não contam).</summary>
        public int Count => _tiles.Count;

        /// <summary>
        /// Registra um tile. Lança em sobreposição (duas células na mesma coordenada) — invariante que
        /// o bake também valida, mas garantida aqui para construção manual nos testes.
        /// </summary>
        public void AddTile(Tile tile)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (_tiles.ContainsKey(tile.Coord))
                throw new InvalidOperationException($"Sobreposição de tiles na coordenada {tile.Coord}.");
            _tiles.Add(tile.Coord, tile);
        }

        public bool HasTile(GridCoord coord) => _tiles.ContainsKey(coord);

        /// <summary>Tile na coordenada, ou <c>null</c> se for um buraco.</summary>
        public Tile GetTile(GridCoord coord) => _tiles.TryGetValue(coord, out var tile) ? tile : null;

        /// <summary>Todos os tiles (ordem não-determinística — não usar em lógica de jogo sensível a ordem).</summary>
        public IReadOnlyCollection<Tile> AllTiles => _tiles.Values;

        /// <summary>
        /// Vizinhos <b>existentes</b> em ordem fixa (N, E, S, W e, se <paramref name="diagonal"/>,
        /// NE, SE, SW, NW). Buracos são ignorados. A ordem fixa é essencial para replay reproduzível.
        /// </summary>
        public IEnumerable<GridCoord> GetNeighbors(GridCoord coord, bool diagonal)
        {
            foreach (var offset in GridCoord.OrthogonalOffsets)
            {
                var n = coord + offset;
                if (_tiles.ContainsKey(n)) yield return n;
            }

            if (!diagonal) yield break;

            foreach (var offset in GridCoord.DiagonalOffsets)
            {
                var n = coord + offset;
                if (_tiles.ContainsKey(n)) yield return n;
            }
        }
    }
}
