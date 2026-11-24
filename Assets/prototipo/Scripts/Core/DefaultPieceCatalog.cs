using System.Collections.Generic;
using UnityEngine;

namespace Prototipo.Core
{
    /// <summary>
    /// Catálogo padrão com os 3 tipos de cápsulas/monstros do protótipo.
    /// </summary>
    public static class DefaultPieceCatalog
    {
        public static PieceSpec CreateRedSpec()
        {
            return new PieceSpec(
                id: "red",
                displayName: "Guardião Vermelho",
                roleSummary: "Tanque corpo-a-corpo (1 casa ortogonal)",
                pieceColor: new Color(0.88f, 0.22f, 0.22f),
                cylinderScale: new Vector3(0.72f, 0.55f, 0.72f),
                maxHP: 12,
                attack: 5,
                defense: 2,
                unlockCost: 3,
                moveCost: 2,
                attackCost: 2,
                attackRange: 1,
                canJumpObstacles: false,
                movePatterns: new MoveStepSequence[]
                {
                    new MoveStepSequence("Norte 1", new BoardCoord(0, 1)),
                    new MoveStepSequence("Sul 1", new BoardCoord(0, -1)),
                    new MoveStepSequence("Leste 1", new BoardCoord(1, 0)),
                    new MoveStepSequence("Oeste 1", new BoardCoord(-1, 0))
                });
        }

        public static PieceSpec CreateBlueSpec()
        {
            return new PieceSpec(
                id: "blue",
                displayName: "Lanceiro Azul",
                roleSummary: "Flanqueador ágil (até 2 casas ortogonais + diagonais frontais)",
                pieceColor: new Color(0.20f, 0.50f, 0.95f),
                cylinderScale: new Vector3(0.52f, 0.80f, 0.52f),
                maxHP: 8,
                attack: 4,
                defense: 1,
                unlockCost: 2,
                moveCost: 1,
                attackCost: 2,
                attackRange: 1,
                canJumpObstacles: false,
                movePatterns: new MoveStepSequence[]
                {
                    new MoveStepSequence("Norte 1", new BoardCoord(0, 1)),
                    new MoveStepSequence("Norte 2", new BoardCoord(0, 1), new BoardCoord(0, 2)),
                    new MoveStepSequence("Sul 1", new BoardCoord(0, -1)),
                    new MoveStepSequence("Sul 2", new BoardCoord(0, -1), new BoardCoord(0, -2)),
                    new MoveStepSequence("Leste 1", new BoardCoord(1, 0)),
                    new MoveStepSequence("Leste 2", new BoardCoord(1, 0), new BoardCoord(2, 0)),
                    new MoveStepSequence("Oeste 1", new BoardCoord(-1, 0)),
                    new MoveStepSequence("Oeste 2", new BoardCoord(-1, 0), new BoardCoord(-2, 0)),
                    new MoveStepSequence("Nordeste 1", new BoardCoord(1, 1)),
                    new MoveStepSequence("Noroeste 1", new BoardCoord(-1, 1))
                });
        }

        public static PieceSpec CreateYellowSpec()
        {
            return new PieceSpec(
                id: "yellow",
                displayName: "Sentinela Amarela",
                roleSummary: "Atirador tático (diagonais até 2 casas + recuo; ataque alcance 3)",
                pieceColor: new Color(0.95f, 0.80f, 0.18f),
                cylinderScale: new Vector3(0.58f, 0.68f, 0.58f),
                maxHP: 6,
                attack: 4,
                defense: 0,
                unlockCost: 2,
                moveCost: 2,
                attackCost: 2,
                attackRange: 3,
                canJumpObstacles: false,
                movePatterns: new MoveStepSequence[]
                {
                    new MoveStepSequence("Nordeste 1", new BoardCoord(1, 1)),
                    new MoveStepSequence("Nordeste 2", new BoardCoord(1, 1), new BoardCoord(2, 2)),
                    new MoveStepSequence("Noroeste 1", new BoardCoord(-1, 1)),
                    new MoveStepSequence("Noroeste 2", new BoardCoord(-1, 1), new BoardCoord(-2, 2)),
                    new MoveStepSequence("Sudeste 1", new BoardCoord(1, -1)),
                    new MoveStepSequence("Sudeste 2", new BoardCoord(1, -1), new BoardCoord(2, -2)),
                    new MoveStepSequence("Sudoeste 1", new BoardCoord(-1, -1)),
                    new MoveStepSequence("Sudoeste 2", new BoardCoord(-1, -1), new BoardCoord(-2, -2)),
                    new MoveStepSequence("Recuo 1", new BoardCoord(0, -1))
                });
        }

        public static List<PieceSpec> CreateDefaultSpecs()
        {
            return new List<PieceSpec>
            {
                CreateRedSpec(),
                CreateBlueSpec(),
                CreateYellowSpec()
            };
        }
    }
}
