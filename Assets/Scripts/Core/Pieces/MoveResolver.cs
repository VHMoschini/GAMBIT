using System.Collections.Generic;
using Game.Core.Grid;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Política de movimento (v6): <b>padrões fixos por peça</b>, <b>deslizar com caminho inteiro livre</b>,
    /// <b>frente relativa ao dono</b>. Função pura sobre topologia + ocupação do grid — sem render, sem evento.
    ///
    /// <para>Reusada por dois pontos: o controller, para destacar os <b>destinos jogáveis</b>
    /// (<c>ReachableTilesComputedEvent</c>), e o <c>MoveCommand.Validate</c>, para revalidar a jogada
    /// escolhida. "Grid fornece topologia; peça/regra fornece a política" (arquitetura, seções 3 e 6).</para>
    /// </summary>
    public static class MoveResolver
    {
        /// <summary>
        /// Espelha um passo canônico conforme o dono. <see cref="PlayerId.Player1"/> usa a orientação
        /// canônica; <see cref="PlayerId.Player2"/> recebe rotação de 180° (frente oposta).
        /// </summary>
        public static GridCoord Orient(GridCoord step, PlayerId owner)
            => owner == PlayerId.Player1 ? step : new GridCoord(-step.X, -step.Z);

        /// <summary>
        /// Resolve a trajetória absoluta de um padrão a partir de <paramref name="origin"/>. Só retorna
        /// <c>true</c> (deslizar) se <b>todas</b> as casas existem, não são bloqueadas por terreno e estão
        /// livres de ocupante. O destino é <c>path[path.Count - 1]</c>.
        /// </summary>
        public static bool TryResolvePath(
            MovePattern pattern, PlayerId owner, GridCoord origin, GridSystem grid,
            out IReadOnlyList<GridCoord> path)
        {
            path = null;
            if (pattern == null || pattern.Steps.Count == 0) return false;

            var absolute = new List<GridCoord>(pattern.Steps.Count);
            foreach (var step in pattern.Steps)
            {
                var coord = origin + Orient(step, owner);
                if (!IsFree(grid, coord)) return false; // casa intermediária bloqueada invalida o padrão inteiro
                absolute.Add(coord);
            }

            path = absolute;
            return true;
        }

        /// <summary>Destinos (último passo) de cada padrão jogável, sem repetição.</summary>
        public static IReadOnlyList<GridCoord> GetPlayableDestinations(
            IReadOnlyList<MovePattern> patterns, PlayerId owner, GridCoord origin, GridSystem grid)
        {
            var destinations = new List<GridCoord>();
            if (patterns == null) return destinations;

            foreach (var pattern in patterns)
                if (TryResolvePath(pattern, owner, origin, grid, out var path))
                {
                    var destination = path[path.Count - 1];
                    if (!destinations.Contains(destination)) destinations.Add(destination);
                }

            return destinations;
        }

        /// <summary><c>true</c> se algum padrão jogável termina em <paramref name="destination"/>.</summary>
        public static bool IsPlayableDestination(
            IReadOnlyList<MovePattern> patterns, PlayerId owner, GridCoord origin, GridSystem grid,
            GridCoord destination)
        {
            if (patterns == null) return false;

            foreach (var pattern in patterns)
                if (TryResolvePath(pattern, owner, origin, grid, out var path)
                    && path[path.Count - 1].Equals(destination))
                    return true;

            return false;
        }

        // ---- Conveniências sobre uma Piece -------------------------------------

        public static IReadOnlyList<GridCoord> GetPlayableDestinations(Piece piece, GridSystem grid)
            => GetPlayableDestinations(piece.Definition.MovePatterns, piece.Owner, piece.Position, grid);

        public static bool IsPlayableDestination(Piece piece, GridSystem grid, GridCoord destination)
            => IsPlayableDestination(piece.Definition.MovePatterns, piece.Owner, piece.Position, grid, destination);

        private static bool IsFree(GridSystem grid, GridCoord coord)
        {
            var tile = grid.GetTile(coord);
            return tile != null && !tile.IsBlocked && !tile.IsOccupied;
        }
    }
}
