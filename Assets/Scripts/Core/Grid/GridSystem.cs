using System;
using System.Collections.Generic;

namespace Game.Core.Grid
{
    /// <summary>
    /// Grid lógico esparso (POCO, testável em edit mode). Mapeia <see cref="GridCoord"/> → <see cref="Tile"/>
    /// num dicionário: <b>buraco = ausência de chave</b>, e o flood-fill contorna buracos sozinho.
    ///
    /// O grid só responde topologia ("quais são meus vizinhos", "o que alcanço"). A <i>política</i> de
    /// movimento (ocupação, diferença de níveis, terreno proibido) vem de fora, no delegate
    /// <c>canStep</c> de <see cref="GetReachable"/>. Um MonoBehaviour fino (<c>GridRuntime</c>) carrega os
    /// dados baked e expõe esta instância.
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

        /// <summary>
        /// BFS puro a partir de <paramref name="origin"/> com custo de passo = 1 (altura não afeta custo,
        /// mas pode bloquear via <paramref name="canStep"/>). Retorna o mapa coord → nº de passos para
        /// cada célula alcançável (inclui a origem com 0). Contorna buracos por construção (vizinho
        /// inexistente nunca entra na fila).
        /// </summary>
        /// <param name="origin">Célula de partida (deve existir).</param>
        /// <param name="maxSteps">Alcance máximo em passos.</param>
        /// <param name="canStep">
        /// Política de transição: <c>canStep(de, para)</c> decide se o passo entre dois tiles vizinhos é
        /// permitido (ocupação, diferença de níveis, terreno). <c>null</c> = só topologia (tudo liberado).
        /// </param>
        /// <param name="diagonal">Se o movimento considera as 8 direções.</param>
        public IReadOnlyDictionary<GridCoord, int> GetReachable(
            GridCoord origin,
            int maxSteps,
            Func<Tile, Tile, bool> canStep = null,
            bool diagonal = false)
        {
            var dist = new Dictionary<GridCoord, int>();
            if (maxSteps < 0 || !_tiles.ContainsKey(origin))
                return dist;

            dist[origin] = 0;
            var queue = new Queue<GridCoord>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDist = dist[current];
                if (currentDist == maxSteps) continue;

                var currentTile = _tiles[current];
                foreach (var next in GetNeighbors(current, diagonal))
                {
                    if (dist.ContainsKey(next)) continue;
                    if (canStep != null && !canStep(currentTile, _tiles[next])) continue;

                    dist[next] = currentDist + 1;
                    queue.Enqueue(next);
                }
            }

            return dist;
        }
    }
}
