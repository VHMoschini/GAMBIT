using Game.Core.Grid;
using Game.Core.Pieces;

namespace Game.Core.Events
{
    /// <summary>
    /// Uma peça morreu (vida chegou a zero) e saiu do tabuleiro. <b>Fato completo:</b> carrega
    /// <see cref="Owner"/> e <see cref="At"/> porque a ocupação/registro do core já foram limpos quando o
    /// visual for reagir — o visual remove a representação a partir do payload, nunca do estado atual.
    /// </summary>
    public readonly struct PieceDiedEvent : IGameEvent
    {
        public readonly PieceId Piece;
        public readonly PlayerId Owner;
        public readonly GridCoord At;

        public PieceDiedEvent(PieceId piece, PlayerId owner, GridCoord at)
        {
            Piece = piece;
            Owner = owner;
            At = at;
        }

        public override string ToString() => $"PieceDied({Piece}, owner {Owner}, at {At})";
    }
}
