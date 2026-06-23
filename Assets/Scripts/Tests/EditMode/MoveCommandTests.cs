using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;
using GenericEventBus;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class MoveCommandTests
    {
        private static GridSystem FullGrid(int width, int height)
        {
            var grid = new GridSystem();
            for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                grid.AddTile(new Tile(new GridCoord(x, z), 0, null));
            return grid;
        }

        // Peão simples: um passo à frente (0,1).
        private static PieceDefinition PawnDef()
            => PieceDefinition.Create("Pawn", new[] { new MovePattern("frente", new GridCoord(0, 1)) });

        private static (GridSystem grid, GameContext ctx, GameEventBus bus) NewMatch()
        {
            var grid = FullGrid(3, 3);
            var bus = new GameEventBus();
            return (grid, new GameContext(grid, bus), bus);
        }

        [Test]
        public void Validate_ValidMove_Ok_AndExecuteMovesOccupant_RaisingFactWithFrom()
        {
            var (grid, ctx, bus) = NewMatch();
            var piece = new Piece(PawnDef(), new PieceId(1), PlayerId.Player1, new GridCoord(1, 0));
            grid.SetOccupant(new GridCoord(1, 0), piece);

            PieceMovedEvent? captured = null;
            GenericEventBus<IGameEvent>.EventHandler<PieceMovedEvent> handler = (ref PieceMovedEvent e) => captured = e;
            bus.SubscribeTo(handler);

            var command = new MoveCommand(PlayerId.Player1, new PieceId(1), new GridCoord(1, 0), new GridCoord(1, 1));

            Assert.IsTrue(command.Validate(ctx).IsValid);
            command.Execute(ctx);

            Assert.IsNull(grid.GetTile(new GridCoord(1, 0)).Occupant, "origem deve ficar livre");
            Assert.AreSame(piece, grid.GetTile(new GridCoord(1, 1)).Occupant, "destino deve receber a peça");
            Assert.AreEqual(new GridCoord(1, 1), piece.Position);

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(new GridCoord(1, 0), captured.Value.From);
            Assert.AreEqual(new GridCoord(1, 1), captured.Value.To);
        }

        [Test]
        public void Validate_WrongOwner_Rejected()
        {
            var (grid, ctx, _) = NewMatch();
            grid.SetOccupant(new GridCoord(1, 0),
                new Piece(PawnDef(), new PieceId(1), PlayerId.Player1, new GridCoord(1, 0)));

            var command = new MoveCommand(PlayerId.Player2, new PieceId(1), new GridCoord(1, 0), new GridCoord(1, 1));
            Assert.IsFalse(command.Validate(ctx).IsValid);
        }

        [Test]
        public void Validate_NonPlayableDestination_Rejected()
        {
            var (grid, ctx, _) = NewMatch();
            grid.SetOccupant(new GridCoord(1, 0),
                new Piece(PawnDef(), new PieceId(1), PlayerId.Player1, new GridCoord(1, 0)));

            // (2,2) não é destino de nenhum padrão do peão.
            var command = new MoveCommand(PlayerId.Player1, new PieceId(1), new GridCoord(1, 0), new GridCoord(2, 2));
            Assert.IsFalse(command.Validate(ctx).IsValid);
        }

        [Test]
        public void Validate_NoPieceAtOrigin_Rejected()
        {
            var (_, ctx, _) = NewMatch();
            var command = new MoveCommand(PlayerId.Player1, new PieceId(1), new GridCoord(1, 0), new GridCoord(1, 1));
            Assert.IsFalse(command.Validate(ctx).IsValid);
        }
    }
}
