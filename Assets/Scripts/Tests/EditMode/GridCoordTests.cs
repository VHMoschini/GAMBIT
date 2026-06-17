using System.Collections.Generic;
using Game.Core.Grid;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class GridCoordTests
    {
        [Test]
        public void Equality_IsByValue()
        {
            Assert.AreEqual(new GridCoord(2, 3), new GridCoord(2, 3));
            Assert.AreNotEqual(new GridCoord(2, 3), new GridCoord(3, 2));
            Assert.IsTrue(new GridCoord(1, 1) == new GridCoord(1, 1));
            Assert.IsTrue(new GridCoord(1, 1) != new GridCoord(1, 2));
        }

        [Test]
        public void HashCode_WorksAsDictionaryKey()
        {
            var map = new Dictionary<GridCoord, string>
            {
                [new GridCoord(0, 0)] = "origin",
                [new GridCoord(5, -2)] = "far",
            };

            Assert.AreEqual("origin", map[new GridCoord(0, 0)]);
            Assert.AreEqual("far", map[new GridCoord(5, -2)]);
            Assert.IsFalse(map.ContainsKey(new GridCoord(1, 1)));
        }

        [Test]
        public void Arithmetic_AddsAndSubtracts()
        {
            Assert.AreEqual(new GridCoord(3, 5), new GridCoord(1, 2) + new GridCoord(2, 3));
            Assert.AreEqual(new GridCoord(-1, -1), new GridCoord(1, 2) - new GridCoord(2, 3));
        }

        [Test]
        public void Distances_ManhattanAndChebyshev()
        {
            var a = new GridCoord(0, 0);
            var b = new GridCoord(3, 2);
            Assert.AreEqual(5, a.ManhattanDistanceTo(b));
            Assert.AreEqual(3, a.ChebyshevDistanceTo(b));
        }

        [Test]
        public void NeighborOffsets_HaveFixedOrder()
        {
            // Ordem fixa essencial para replay reproduzível: N, E, S, W.
            CollectionAssert.AreEqual(
                new[] { new GridCoord(0, 1), new GridCoord(1, 0), new GridCoord(0, -1), new GridCoord(-1, 0) },
                GridCoord.OrthogonalOffsets);
            // NE, SE, SW, NW.
            CollectionAssert.AreEqual(
                new[] { new GridCoord(1, 1), new GridCoord(1, -1), new GridCoord(-1, -1), new GridCoord(-1, 1) },
                GridCoord.DiagonalOffsets);
        }
    }
}
