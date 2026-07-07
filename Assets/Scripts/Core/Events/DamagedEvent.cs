using Game.Core.Pieces;

namespace Game.Core.Events
{
    /// <summary>
    /// Uma peça sofreu dano. <b>Fato completo:</b> carrega <see cref="Amount"/> e o par
    /// <see cref="HpBefore"/>/<see cref="HpAfter"/> porque a vida do core já avançou quando o visual for
    /// apresentar — o visual anima a partir do payload, nunca lê o <c>CurrentHP</c> atual.
    /// </summary>
    public readonly struct DamagedEvent : IGameEvent
    {
        public readonly PieceId Target;
        public readonly int Amount;
        public readonly int HpBefore;
        public readonly int HpAfter;

        public DamagedEvent(PieceId target, int amount, int hpBefore, int hpAfter)
        {
            Target = target;
            Amount = amount;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
        }

        public override string ToString() => $"Damaged({Target} -{Amount}, {HpBefore}->{HpAfter})";
    }
}
