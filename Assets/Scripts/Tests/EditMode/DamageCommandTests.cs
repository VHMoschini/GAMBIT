using System;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;
using GenericEventBus;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DamageCommandTests
    {
        private static GridSystem FullGrid(int width, int height)
        {
            var grid = new GridSystem();
            for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                grid.AddTile(new Tile(new GridCoord(x, z), 0, null));
            return grid;
        }

        private static Piece PlaceRegistered(
            GridSystem grid, PieceRegistry registry, int id, GridCoord at, int maxHP, int defense = 0)
        {
            var def = PieceDefinition.Create("P", Array.Empty<MovePattern>(), maxHP: maxHP, defense: defense);
            var piece = new Piece(def, new PieceId(id), PlayerId.Player2, at);
            grid.SetOccupant(at, piece);
            registry.Register(piece);
            return piece;
        }

        [Test]
        public void Execute_NonLethal_AppliesDamage_AndRaisesDamagedFact()
        {
            var grid = FullGrid(3, 3);
            var registry = new PieceRegistry();
            var bus = new GameEventBus();
            var ctx = new GameContext(grid, bus, registry);
            var piece = PlaceRegistered(grid, registry, 1, new GridCoord(1, 1), maxHP: 5);

            DamagedEvent? damaged = null;
            GenericEventBus<IGameEvent>.EventHandler<DamagedEvent> onDamaged = (ref DamagedEvent e) => damaged = e;
            bus.SubscribeTo(onDamaged);

            new DamageCommand(new PieceId(1), 2).Execute(ctx);

            Assert.AreEqual(3, piece.CurrentHP);
            Assert.IsTrue(piece.IsAlive);
            Assert.IsTrue(damaged.HasValue);
            Assert.AreEqual(5, damaged.Value.HpBefore);
            Assert.AreEqual(3, damaged.Value.HpAfter);
            Assert.AreEqual(2, damaged.Value.Amount);
            // Não morreu → segue no grid e no registro.
            Assert.AreSame(piece, grid.GetTile(new GridCoord(1, 1)).Occupant);
            Assert.AreSame(piece, registry.Get(new PieceId(1)));
        }

        [Test]
        public void Execute_Lethal_ClearsOccupant_Unregisters_AndRaisesDiedAfterDamaged()
        {
            var grid = FullGrid(3, 3);
            var registry = new PieceRegistry();
            var bus = new GameEventBus();
            var ctx = new GameContext(grid, bus, registry);
            var at = new GridCoord(1, 1);
            PlaceRegistered(grid, registry, 1, at, maxHP: 2);

            var order = new System.Collections.Generic.List<string>();
            GenericEventBus<IGameEvent>.EventHandler<DamagedEvent> onDamaged = (ref DamagedEvent e) => order.Add("damaged");
            PieceDiedEvent? died = null;
            GenericEventBus<IGameEvent>.EventHandler<PieceDiedEvent> onDied = (ref PieceDiedEvent e) => { order.Add("died"); died = e; };
            bus.SubscribeTo(onDamaged);
            bus.SubscribeTo(onDied);

            new DamageCommand(new PieceId(1), 5).Execute(ctx);

            Assert.IsNull(grid.GetTile(at).Occupant, "morte deve liberar a casa");
            Assert.IsNull(registry.Get(new PieceId(1)), "morte deve desregistrar a peça");
            Assert.AreEqual(new[] { "damaged", "died" }, order.ToArray(), "dano antes da morte");
            Assert.IsTrue(died.HasValue);
            Assert.AreEqual(PlayerId.Player2, died.Value.Owner);
            Assert.AreEqual(at, died.Value.At);
        }

        [Test]
        public void Validate_TargetNotRegistered_Rejected()
        {
            var grid = FullGrid(3, 3);
            var ctx = new GameContext(grid, new GameEventBus(), new PieceRegistry());

            var result = new DamageCommand(new PieceId(99), 1).Validate(ctx);
            Assert.IsFalse(result.IsValid);
        }
    }
}
