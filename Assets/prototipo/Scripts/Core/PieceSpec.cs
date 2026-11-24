using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototipo.Core
{
    /// <summary>
    /// Especificação imutável de um tipo de criatura/cápsula no protótipo.
    /// </summary>
    public sealed class PieceSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string RoleSummary { get; }
        public Color PieceColor { get; }
        public Vector3 CylinderScale { get; }
        public int MaxHP { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int UnlockCost { get; }
        public int MoveCost { get; }
        public int AttackCost { get; }
        public int AttackRange { get; }
        public bool CanJumpObstacles { get; }
        public IReadOnlyList<MoveStepSequence> MovePatterns { get; }

        public PieceSpec(
            string id,
            string displayName,
            string roleSummary,
            Color pieceColor,
            Vector3 cylinderScale,
            int maxHP,
            int attack,
            int defense,
            int unlockCost,
            int moveCost,
            int attackCost,
            int attackRange,
            bool canJumpObstacles,
            IEnumerable<MoveStepSequence> movePatterns)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            RoleSummary = roleSummary ?? string.Empty;
            PieceColor = pieceColor;
            CylinderScale = cylinderScale;
            MaxHP = Math.Max(1, maxHP);
            Attack = Math.Max(1, attack);
            Defense = Math.Max(0, defense);
            UnlockCost = Math.Max(0, unlockCost);
            MoveCost = Math.Max(0, moveCost);
            AttackCost = Math.Max(0, attackCost);
            AttackRange = Math.Max(1, attackRange);
            CanJumpObstacles = canJumpObstacles;

            var patterns = new List<MoveStepSequence>();
            if (movePatterns != null)
            {
                foreach (MoveStepSequence pattern in movePatterns)
                {
                    if (pattern != null && pattern.Steps != null && pattern.Steps.Count > 0)
                    {
                        var copiedSteps = new BoardCoord[pattern.Steps.Count];
                        for (int i = 0; i < pattern.Steps.Count; i++)
                        {
                            copiedSteps[i] = pattern.Steps[i];
                        }

                        patterns.Add(new MoveStepSequence(pattern.Label, copiedSteps));
                    }
                }
            }

            MovePatterns = patterns.AsReadOnly();
        }
    }
}
