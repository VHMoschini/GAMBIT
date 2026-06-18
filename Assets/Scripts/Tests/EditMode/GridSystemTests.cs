using System.Collections.Generic;
using System.Linq;
using Game.Core.Grid;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class GridSystemTests
    {
        // Tiles construídos à mão (sem cena, sem SO): TileType null → IsBlocked false, ótimo para topologia pura.
        private static GridSystem BuildFromCoords(IEnumerable<GridCoord> coords)
        {
            var grid = new GridSystem();
            foreach (var c in coords)
                grid.AddTile(new Tile(c, 0, null));
            return grid;
        }

        /// <summary>Tabuleiro 3x3 cheio, exceto o centro (1,1) — o buraco.</summary>
        private static GridSystem BuildThreeByThreeWithHole()
        {
            var coords = new List<GridCoord>();
            for (var x = 0; x < 3; x++)
            for (var z = 0; z < 3; z++)
                if (!(x == 1 && z == 1))
                    coords.Add(new GridCoord(x, z));
            return BuildFromCoords(coords);
        }

        [Test]
        public void AddTile_Duplicate_Throws()
        {
            var grid = new GridSystem();
            grid.AddTile(new Tile(new GridCoord(0, 0), 0, null));
            Assert.Throws<System.InvalidOperationException>(
                () => grid.AddTile(new Tile(new GridCoord(0, 0), 1, null)));
        }

        [Test]
        public void HasTile_And_GetTile_RespectHoles()
        {
            var grid = BuildThreeByThreeWithHole();
            Assert.IsTrue(grid.HasTile(new GridCoord(0, 0)));
            Assert.IsNotNull(grid.GetTile(new GridCoord(0, 0)));

            Assert.IsFalse(grid.HasTile(new GridCoord(1, 1))); // o buraco
            Assert.IsNull(grid.GetTile(new GridCoord(1, 1)));
            Assert.AreEqual(8, grid.Count);
        }

        [Test]
        public void GetNeighbors_FixedOrder_IgnoresHolesAndOutOfBounds()
        {
            var grid = BuildThreeByThreeWithHole();

            // Vizinhos de (1,0): N=(1,1) é buraco → fora; E=(2,0); S=(1,-1) inexistente → fora; W=(0,0).
            var neighbors = grid.GetNeighbors(new GridCoord(1, 0), diagonal: false).ToList();
            CollectionAssert.AreEqual(new[] { new GridCoord(2, 0), new GridCoord(0, 0) }, neighbors);
        }

        [Test]
        public void GetNeighbors_Diagonal_AddsDiagonalsAfterOrthogonals()
        {
            var grid = BuildThreeByThreeWithHole();

            // De (0,0): orto N=(0,1), E=(1,0); diagonais NE=(1,1) é buraco → fora.
            var neighbors = grid.GetNeighbors(new GridCoord(0, 0), diagonal: true).ToList();
            CollectionAssert.AreEqual(new[] { new GridCoord(0, 1), new GridCoord(1, 0) }, neighbors);
        }
    }
}
