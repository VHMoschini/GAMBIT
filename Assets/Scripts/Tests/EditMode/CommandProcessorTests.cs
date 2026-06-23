using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;
using GenericEventBus;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class CommandProcessorTests
    {
        private static GridSystem FullGrid(int width, int height)
        {
            var grid = new GridSystem();
            for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                grid.AddTile(new Tile(new GridCoord(x, z), 0, null));
            return grid;
        }

        private static PieceDefinition PawnDef()
            => PieceDefinition.Create("Pawn", new[] { new MovePattern("frente", new GridCoord(0, 1)) });

        [Test]
        public void Submit_ValidMove_LogsCommand_AndRaisesMovedEvent()
        {
            var grid = FullGrid(3, 3);
            var bus = new GameEventBus();
            var processor = new CommandProcessor(new GameContext(grid, bus));
            grid.SetOccupant(new GridCoord(1, 0),
                new Piece(PawnDef(), new PieceId(1), PlayerId.Player1, new GridCoord(1, 0)));

            var moved = false;
            GenericEventBus<IGameEvent>.EventHandler<PieceMovedEvent> handler = (ref PieceMovedEvent e) => moved = true;
            bus.SubscribeTo(handler);

            var result = processor.Submit(
                new MoveCommand(PlayerId.Player1, new PieceId(1), new GridCoord(1, 0), new GridCoord(1, 1)));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(1, processor.History.Count);
            Assert.IsTrue(moved);
        }

        [Test]
        public void Submit_Invalid_RaisesRejected_AndDoesNotLog()
        {
            var grid = FullGrid(3, 3);
            var bus = new GameEventBus();
            var processor = new CommandProcessor(new GameContext(grid, bus));
            grid.SetOccupant(new GridCoord(1, 0),
                new Piece(PawnDef(), new PieceId(1), PlayerId.Player1, new GridCoord(1, 0)));

            var rejected = false;
            GenericEventBus<IGameEvent>.EventHandler<CommandRejectedEvent> handler = (ref CommandRejectedEvent e) => rejected = true;
            bus.SubscribeTo(handler);

            // Dono errado → recusado na Camada 2.
            var result = processor.Submit(
                new MoveCommand(PlayerId.Player2, new PieceId(1), new GridCoord(1, 0), new GridCoord(1, 1)));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(0, processor.History.Count);
            Assert.IsTrue(rejected);
        }
    }
}
