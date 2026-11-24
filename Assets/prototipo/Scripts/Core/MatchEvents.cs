namespace Prototipo.Core
{
    /// <summary>
    /// Dados do evento de movimento de peça.
    /// </summary>
    public readonly struct PieceMovedData
    {
        public int PieceId { get; }
        public BoardCoord From { get; }
        public BoardCoord To { get; }

        public PieceMovedData(int pieceId, BoardCoord from, BoardCoord to)
        {
            PieceId = pieceId;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// Dados do evento de ataque entre peças.
    /// </summary>
    public readonly struct PieceAttackedData
    {
        public int AttackerId { get; }
        public int TargetId { get; }
        public BoardCoord AttackerPos { get; }
        public BoardCoord TargetPos { get; }
        public int Damage { get; }
        public int TargetHpBefore { get; }
        public int TargetHpAfter { get; }
        public bool TargetDied { get; }

        public PieceAttackedData(
            int attackerId,
            int targetId,
            BoardCoord attackerPos,
            BoardCoord targetPos,
            int damage,
            int targetHpBefore,
            int targetHpAfter,
            bool targetDied)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            AttackerPos = attackerPos;
            TargetPos = targetPos;
            Damage = damage;
            TargetHpBefore = targetHpBefore;
            TargetHpAfter = targetHpAfter;
            TargetDied = targetDied;
        }
    }

    /// <summary>
    /// Dados do evento de desbloqueio de cápsula.
    /// </summary>
    public readonly struct CapsuleUnlockedData
    {
        public int PieceId { get; }
        public PlayerTeam Owner { get; }
        public BoardCoord Position { get; }
        public int CostAP { get; }

        public CapsuleUnlockedData(int pieceId, PlayerTeam owner, BoardCoord position, int costAP)
        {
            PieceId = pieceId;
            Owner = owner;
            Position = position;
            CostAP = costAP;
        }
    }
}
