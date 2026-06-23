using Game.Core.Grid;
using Game.Core.Pieces;

namespace Game.Core.Events
{
    /// <summary>
    /// Uma peça mudou de casa. <b>Fato completo:</b> carrega <see cref="From"/> e <see cref="To"/> porque
    /// a ocupação do grid já avançou quando o visual for animar — o visual lê o payload, nunca o estado atual.
    /// </summary>
    public readonly struct PieceMovedEvent : IGameEvent
    {
        public readonly PieceId Piece;
        public readonly GridCoord From;
        public readonly GridCoord To;

        public PieceMovedEvent(PieceId piece, GridCoord from, GridCoord to)
        {
            Piece = piece;
            From = from;
            To = to;
        }

        public override string ToString() => $"PieceMoved({Piece} {From}->{To})";
    }
}
