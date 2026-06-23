using System.Collections.Generic;
using Game.Core.Grid;
using Game.Core.Pieces;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class MoveResolverTests
    {
        // Grid retangular cheio (TileType null → não bloqueia), exceto coordenadas em 'holes'.
        private static GridSystem Rectangle(int width, int height, params GridCoord[] holes)
        {
            var holeSet = new HashSet<GridCoord>(holes);
            var grid = new GridSystem();
            for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
            {
                var coord = new GridCoord(x, z);
                if (!holeSet.Contains(coord)) grid.AddTile(new Tile(coord, 0, null));
            }
            return grid;
        }

        // "2 pra frente" canônico: atravessa (0,1) e termina em (0,2).
        private static MovePattern Forward2() => new MovePattern("2 frente", new GridCoord(0, 1), new GridCoord(0, 2));

        private static Piece PieceAt(GridCoord at, PlayerId owner = PlayerId.Player1)
            => new Piece(PieceDefinition.Create("x", new[] { Forward2() }), new PieceId(99), owner, at);

        [Test]
        public void Player1_ForwardPattern_DestinationIsFinalStep()
        {
            var grid = Rectangle(3, 5);
            var destinations = MoveResolver.GetPlayableDestinations(
                new[] { Forward2() }, PlayerId.Player1, new GridCoord(1, 0), grid);

            CollectionAssert.AreEqual(new[] { new GridCoord(1, 2) }, destinations);
        }

        [Test]
        public void Player2_ForwardPattern_IsMirrored_180()
        {
            var grid = Rectangle(3, 5);
            // Player2 "à frente" aponta para -Z: de (1,4) o destino é (1,2).
            var destinations = MoveResolver.GetPlayableDestinations(
                new[] { Forward2() }, PlayerId.Player2, new GridCoord(1, 4), grid);

            CollectionAssert.AreEqual(new[] { new GridCoord(1, 2) }, destinations);
        }

        [Test]
        public void OccupiedIntermediate_InvalidatesWholePattern_Slide()
        {
            var grid = Rectangle(3, 5);
            grid.SetOccupant(new GridCoord(1, 1), PieceAt(new GridCoord(1, 1))); // bloqueia o meio do caminho

            Assert.IsFalse(MoveResolver.IsPlayableDestination(
                new[] { Forward2() }, PlayerId.Player1, new GridCoord(1, 0), grid, new GridCoord(1, 2)));
        }

        [Test]
        public void HoleInPath_InvalidatesPattern()
        {
            var grid = Rectangle(3, 5, new GridCoord(1, 2)); // destino inexistente (buraco)

            var destinations = MoveResolver.GetPlayableDestinations(
                new[] { Forward2() }, PlayerId.Player1, new GridCoord(1, 0), grid);

            CollectionAssert.IsEmpty(destinations);
        }

        [Test]
        public void OffGridDestination_NotPlayable()
        {
            var grid = Rectangle(3, 2); // só z = 0,1 → (1,2) não existe

            var destinations = MoveResolver.GetPlayableDestinations(
                new[] { Forward2() }, PlayerId.Player1, new GridCoord(1, 0), grid);

            CollectionAssert.IsEmpty(destinations);
        }
    }
}
