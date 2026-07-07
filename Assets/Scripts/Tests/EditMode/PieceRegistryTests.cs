using System;
using Game.Core.Grid;
using Game.Core.Pieces;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class PieceRegistryTests
    {
        private static Piece NewPiece(int id)
        {
            var def = PieceDefinition.Create("P", Array.Empty<MovePattern>(), maxHP: 3);
            return new Piece(def, new PieceId(id), PlayerId.Player1, new GridCoord(0, 0));
        }

        [Test]
        public void Register_ThenGet_ResolvesById()
        {
            var registry = new PieceRegistry();
            var piece = NewPiece(1);
            registry.Register(piece);

            Assert.AreSame(piece, registry.Get(new PieceId(1)));
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void Get_Unregistered_ReturnsNull()
        {
            var registry = new PieceRegistry();
            Assert.IsNull(registry.Get(new PieceId(42)));
        }

        [Test]
        public void Unregister_RemovesFromRegistry()
        {
            var registry = new PieceRegistry();
            registry.Register(NewPiece(1));
            registry.Unregister(new PieceId(1));

            Assert.IsNull(registry.Get(new PieceId(1)));
            Assert.AreEqual(0, registry.Count);
        }

        [Test]
        public void Register_DuplicateId_Throws()
        {
            var registry = new PieceRegistry();
            registry.Register(NewPiece(1));
            Assert.Throws<InvalidOperationException>(() => registry.Register(NewPiece(1)));
        }

        [Test]
        public void Unregister_Unknown_Throws()
        {
            var registry = new PieceRegistry();
            Assert.Throws<InvalidOperationException>(() => registry.Unregister(new PieceId(7)));
        }
    }
}
