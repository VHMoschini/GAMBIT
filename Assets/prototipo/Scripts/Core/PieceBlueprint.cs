using System.Collections.Generic;
using UnityEngine;

namespace Prototipo.Core
{
    /// <summary>
    /// Asset configurável no Inspector para criar ou editar tipos de peças do protótipo.
    /// </summary>
    [CreateAssetMenu(fileName = "PieceBlueprint", menuName = "GAMBIT/Prototipo/Piece Blueprint")]
    public sealed class PieceBlueprint : ScriptableObject
    {
        [SerializeField] private string id = "custom";
        [SerializeField] private string displayName = "Nova Peça";
        [SerializeField] private string roleSummary = string.Empty;
        [SerializeField] private Color pieceColor = Color.white;
        [SerializeField] private Vector3 cylinderScale = new Vector3(0.6f, 0.65f, 0.6f);
        [SerializeField] private int maxHP = 8;
        [SerializeField] private int attack = 4;
        [SerializeField] private int defense = 1;
        [SerializeField] private int unlockCost = 2;
        [SerializeField] private int moveCost = 2;
        [SerializeField] private int attackCost = 2;
        [SerializeField] private int attackRange = 1;
        [SerializeField] private bool canJumpObstacles;
        [SerializeField] private List<MoveStepSequence> movePatterns = new List<MoveStepSequence>();

        public PieceSpec ToSpec()
        {
            return new PieceSpec(
                id,
                displayName,
                roleSummary,
                pieceColor,
                cylinderScale,
                maxHP,
                attack,
                defense,
                unlockCost,
                moveCost,
                attackCost,
                attackRange,
                canJumpObstacles,
                movePatterns);
        }

        public void ConfigureFromSpec(PieceSpec spec)
        {
            if (spec == null)
            {
                return;
            }

            id = spec.Id;
            displayName = spec.DisplayName;
            roleSummary = spec.RoleSummary;
            pieceColor = spec.PieceColor;
            cylinderScale = spec.CylinderScale;
            maxHP = spec.MaxHP;
            attack = spec.Attack;
            defense = spec.Defense;
            unlockCost = spec.UnlockCost;
            moveCost = spec.MoveCost;
            attackCost = spec.AttackCost;
            attackRange = spec.AttackRange;
            canJumpObstacles = spec.CanJumpObstacles;

            movePatterns = new List<MoveStepSequence>();
            if (spec.MovePatterns != null)
            {
                foreach (MoveStepSequence pattern in spec.MovePatterns)
                {
                    if (pattern == null || pattern.Steps == null)
                    {
                        continue;
                    }

                    var steps = new BoardCoord[pattern.Steps.Count];
                    for (int i = 0; i < pattern.Steps.Count; i++)
                    {
                        steps[i] = pattern.Steps[i];
                    }

                    movePatterns.Add(new MoveStepSequence(pattern.Label, steps));
                }
            }
        }
    }
}
