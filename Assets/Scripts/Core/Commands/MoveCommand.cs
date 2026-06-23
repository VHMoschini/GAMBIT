using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;

namespace Game.Core.Commands
{
    /// <summary>
    /// Move uma peça por um padrão jogável (deslizar: caminho inteiro livre). Autossuficiente — carrega
    /// <see cref="Owner"/>/<see cref="Piece"/>/<see cref="From"/>/<see cref="To"/>, re-executável a partir do log.
    ///
    /// <para><see cref="Validate"/> revalida sempre (o controller pode estar enganado ou o estado pode ter
    /// mudado): posse, e que <see cref="To"/> é o destino de um <see cref="MovePattern"/> com caminho livre.</para>
    /// </summary>
    public sealed class MoveCommand : ICommand
    {
        public PlayerId Owner { get; }
        public PieceId Piece { get; }
        public GridCoord From { get; }
        public GridCoord To { get; }

        public MoveCommand(PlayerId owner, PieceId piece, GridCoord from, GridCoord to)
        {
            Owner = owner;
            Piece = piece;
            From = from;
            To = to;
        }

        public Result Validate(GameContext context)
        {
            var tile = context.Grid.GetTile(From);
            if (tile == null) return Result.Reject($"Origem {From} não existe no grid.");

            var piece = tile.Occupant;
            if (piece == null) return Result.Reject($"Nenhuma peça na origem {From}.");
            if (piece.Id != Piece) return Result.Reject($"Peça em {From} não corresponde a {Piece}.");
            if (piece.Owner != Owner) return Result.Reject($"Peça {Piece} não pertence a {Owner}.");

            if (!MoveResolver.IsPlayableDestination(piece, context.Grid, To))
                return Result.Reject($"{To} não é destino de um movimento jogável (sem caminho livre).");

            return Result.Ok();
        }

        public void Execute(GameContext context)
        {
            var piece = context.Grid.GetTile(From).Occupant;

            context.Grid.ClearOccupant(From);
            context.Grid.SetOccupant(To, piece);
            piece.MoveTo(To);

            // Fato completo: o payload carrega o 'from' porque a ocupação já avançou quando o visual animar.
            context.Events.Raise(new PieceMovedEvent(Piece, From, To));
        }

        public override string ToString() => $"Move {Piece} {From} -> {To} (owner {Owner})";
    }
}
