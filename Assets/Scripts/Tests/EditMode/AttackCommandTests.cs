using System;
using System.Collections.Generic;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;
using GenericEventBus;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class AttackCommandTests
    {
        private sealed class Match
        {
            public GridSystem Grid;
            public PieceRegistry Registry;
            public GameEventBus Bus;
            public GameContext Context;
            public CommandProcessor Processor;
        }

        private static GridSystem FullGrid(int width, int height)
        {
            var grid = new GridSystem();
            for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                grid.AddTile(new Tile(new GridCoord(x, z), 0, null));
            return grid;
        }

        private static Match NewMatch()
        {
            var grid = FullGrid(4, 4);
            var registry = new PieceRegistry();
            var bus = new GameEventBus();
            var ctx = new GameContext(grid, bus, registry);
            return new Match { Grid = grid, Registry = registry, Bus = bus, Context = ctx, Processor = new CommandProcessor(ctx) };
        }

        private static Piece Place(
            Match m, int id, PlayerId owner, GridCoord at, int attack = 1, int defense = 0, int maxHP = 3)
        {
            var def = PieceDefinition.Create("P", Array.Empty<MovePattern>(), attack: attack, defense: defense, maxHP: maxHP);
            var piece = new Piece(def, new PieceId(id), owner, at);
            m.Grid.SetOccupant(at, piece);
            m.Processor.Register(piece);
            return piece;
        }

        [Test]
        public void Validate_ValidEnemyInRange_Ok()
        {
            var m = NewMatch();
            Place(m, 1, PlayerId.Player1, new GridCoord(1, 1));
            Place(m, 2, PlayerId.Player2, new GridCoord(1, 2));

            var cmd = new AttackCommand(PlayerId.Player1, new PieceId(1), new PieceId(2), range: 1);
            Assert.IsTrue(cmd.Validate(m.Context).IsValid);
        }

        [Test]
        public void Validate_AllyTarget_Rejected()
        {
            var m = NewMatch();
            Place(m, 1, PlayerId.Player1, new GridCoord(1, 1));
            Place(m, 2, PlayerId.Player1, new GridCoord(1, 2));

            var cmd = new AttackCommand(PlayerId.Player1, new PieceId(1), new PieceId(2), range: 1);
            Assert.IsFalse(cmd.Validate(m.Context).IsValid);
        }

        [Test]
        public void Validate_WrongOwner_Rejected()
        {
            var m = NewMatch();
            Place(m, 1, PlayerId.Player1, new GridCoord(1, 1));
            Place(m, 2, PlayerId.Player2, new GridCoord(1, 2));

            var cmd = new AttackCommand(PlayerId.Player2, new PieceId(1), new PieceId(2), range: 1);
            Assert.IsFalse(cmd.Validate(m.Context).IsValid);
        }

        [Test]
        public void Validate_OutOfRange_Rejected()
        {
            var m = NewMatch();
            Place(m, 1, PlayerId.Player1, new GridCoord(0, 0));
            Place(m, 2, PlayerId.Player2, new GridCoord(3, 3));

            var cmd = new AttackCommand(PlayerId.Player1, new PieceId(1), new PieceId(2), range: 1);
            Assert.IsFalse(cmd.Validate(m.Context).IsValid);
        }

        [Test]
        public void Submit_Attack_ResolvesDerivedChain_DamageThenDeath_InOrder()
        {
            var m = NewMatch();
            Place(m, 1, PlayerId.Player1, new GridCoord(1, 1), attack: 3);
            var targetAt = new GridCoord(1, 2);
            Place(m, 2, PlayerId.Player2, targetAt, defense: 1, maxHP: 2); // 3 - 1 = 2 → morre

            var events = new List<string>();
            GenericEventBus<IGameEvent>.EventHandler<DamagedEvent> onDamaged = (ref DamagedEvent e) => events.Add("damaged");
            GenericEventBus<IGameEvent>.EventHandler<PieceDiedEvent> onDied = (ref PieceDiedEvent e) => events.Add("died");
            m.Bus.SubscribeTo(onDamaged);
            m.Bus.SubscribeTo(onDied);

            var result = m.Processor.Submit(
                new AttackCommand(PlayerId.Player1, new PieceId(1), new PieceId(2), range: 1));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(new[] { "damaged", "died" }, events.ToArray(), "dano resolve antes da morte");
            Assert.IsNull(m.Grid.GetTile(targetAt).Occupant, "alvo morto sai do grid");
            Assert.IsNull(m.Registry.Get(new PieceId(2)), "alvo morto sai do registro");
            // Log tem a jogada e o derivado: Attack seguido de Damage.
            Assert.AreEqual(2, m.Processor.History.Count);
            Assert.IsInstanceOf<AttackCommand>(m.Processor.History[0]);
            Assert.IsInstanceOf<DamageCommand>(m.Processor.History[1]);
        }

        [Test]
        public void Submit_Attack_NonLethal_TargetSurvives_WithDamageLogged()
        {
            var m = NewMatch();
            Place(m, 1, PlayerId.Player1, new GridCoord(1, 1), attack: 2);
            var target = Place(m, 2, PlayerId.Player2, new GridCoord(1, 2), defense: 0, maxHP: 5);

            m.Processor.Submit(new AttackCommand(PlayerId.Player1, new PieceId(1), new PieceId(2), range: 1));

            Assert.AreEqual(3, target.CurrentHP);
            Assert.IsTrue(target.IsAlive);
            Assert.AreSame(target, m.Registry.Get(new PieceId(2)));
            Assert.AreEqual(2, m.Processor.History.Count);
        }
    }
}
