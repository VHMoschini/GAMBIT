using System.Collections.Generic;

namespace Prototipo.Core
{
    /// <summary>
    /// Calcula movimentos e alvos válidos com espelhamento de 180° para o Jogador 2.
    /// </summary>
    public static class MovementCalculator
    {
        /// <summary>
        /// Orienta um passo canônico conforme o dono da peça:
        /// Jogador 1 mantém (x, z); Jogador 2 gira 180° (-x, -z).
        /// </summary>
        public static BoardCoord OrientStep(BoardCoord step, PlayerTeam owner)
        {
            return owner == PlayerTeam.Player1
                ? step
                : new BoardCoord(-step.X, -step.Z);
        }

        /// <summary>
        /// Retorna todas as casas de destino válidas para a peça desbloqueada e viva.
        /// </summary>
        public static IReadOnlyList<BoardCoord> GetValidMoveDestinations(PieceInstance piece, BoardGrid board)
        {
            var destinations = new List<BoardCoord>();
            if (piece == null || board == null || !piece.IsAlive || !piece.IsUnlocked)
            {
                return destinations;
            }

            IReadOnlyList<MoveStepSequence> patterns = piece.Spec.MovePatterns;
            if (patterns == null)
            {
                return destinations;
            }

            for (int p = 0; p < patterns.Count; p++)
            {
                MoveStepSequence pattern = patterns[p];
                if (pattern == null || pattern.Steps == null || pattern.Steps.Count == 0)
                {
                    continue;
                }

                int stepCount = pattern.Steps.Count;
                bool valid = true;
                BoardCoord finalCoord = default(BoardCoord);

                for (int s = 0; s < stepCount; s++)
                {
                    BoardCoord coord = piece.Position + OrientStep(pattern.Steps[s], piece.Owner);
                    BoardTile tile = board.GetTile(coord);
                    bool isLast = s == stepCount - 1;

                    if (isLast)
                    {
                        if (tile == null || !tile.IsWalkable || tile.IsOccupied)
                        {
                            valid = false;
                        }
                        else
                        {
                            finalCoord = coord;
                        }
                    }
                    else if (!piece.Spec.CanJumpObstacles)
                    {
                        if (tile == null || !tile.IsWalkable || tile.IsOccupied)
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                if (valid && !destinations.Contains(finalCoord))
                {
                    destinations.Add(finalCoord);
                }
            }

            return destinations;
        }

        public static bool IsValidMoveDestination(PieceInstance piece, BoardGrid board, BoardCoord destination)
        {
            IReadOnlyList<BoardCoord> valid = GetValidMoveDestinations(piece, board);
            for (int i = 0; i < valid.Count; i++)
            {
                if (valid[i].Equals(destination))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retorna coordenadas de inimigos vivos (abertos ou em cápsula) dentro do alcance de ataque.
        /// </summary>
        public static IReadOnlyList<BoardCoord> GetValidAttackTargets(PieceInstance attacker, BoardGrid board)
        {
            var targets = new List<BoardCoord>();
            if (attacker == null || board == null || !attacker.IsAlive || !attacker.IsUnlocked)
            {
                return targets;
            }

            IReadOnlyList<BoardTile> allTiles = board.AllTiles;
            for (int i = 0; i < allTiles.Count; i++)
            {
                BoardTile tile = allTiles[i];
                if (!tile.IsOccupied)
                {
                    continue;
                }

                PieceInstance occupant = tile.Occupant;
                if (!occupant.IsAlive || occupant.Owner == attacker.Owner)
                {
                    continue;
                }

                if (attacker.Position.ChebyshevDistanceTo(tile.Coord) <= attacker.Spec.AttackRange)
                {
                    targets.Add(tile.Coord);
                }
            }

            return targets;
        }
    }
}
