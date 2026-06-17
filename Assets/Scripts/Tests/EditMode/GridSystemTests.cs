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

        [Test]
        public void GetReachable_ZeroSteps_OnlyOrigin()
        {
            var grid = BuildThreeByThreeWithHole();
            var reach = grid.GetReachable(new GridCoord(0, 0), maxSteps: 0);
            CollectionAssert.AreEqual(new[] { new GridCoord(0, 0) }, reach.Keys);
            Assert.AreEqual(0, reach[new GridCoord(0, 0)]);
        }

        // ---- Destaque do Marco 1: o BFS contorna o buraco ----------------------

        [Test]
        public void GetReachable_BfsDetoursAroundHole()
        {
            // Tabuleiro 3x3 com buraco no centro (1,1). Da origem (0,1) ao alvo (2,1):
            // em linha reta seriam 2 passos, mas (1,1) é buraco → o BFS é OBRIGADO a contornar.
            var grid = BuildThreeByThreeWithHole();
            var origin = new GridCoord(0, 1);
            var target = new GridCoord(2, 1);

            Assert.AreEqual(2, origin.ManhattanDistanceTo(target),
                "Pré-condição: a distância em linha reta é 2.");

            // Com alcance 2, o alvo é INALCANÇÁVEL — prova que o BFS não atravessa o buraco.
            var reachShort = grid.GetReachable(origin, maxSteps: 2);
            Assert.IsFalse(reachShort.ContainsKey(target),
                "Com 2 passos o alvo seria alcançável em linha reta; o buraco impede.");

            // Com alcance 4, é alcançável em EXATAMENTE 4 passos — o comprimento do desvio.
            var reachFull = grid.GetReachable(origin, maxSteps: 4);
            Assert.IsTrue(reachFull.ContainsKey(target), "Contornando o buraco, o alvo é alcançável.");
            Assert.AreEqual(4, reachFull[target], "O desvio em volta do buraco custa 4 passos.");

            // E o buraco jamais aparece no alcance.
            Assert.IsFalse(reachFull.ContainsKey(new GridCoord(1, 1)));
        }

        [Test]
        public void GetReachable_CanStepPolicy_BlocksTransition()
        {
            // Corredor reto de 4 tiles: (0,0)..(3,0). A política proíbe entrar em (2,0),
            // truncando o alcance — "Grid fornece topologia; peça/regra fornece a política".
            var grid = BuildFromCoords(new[]
            {
                new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0), new GridCoord(3, 0),
            });

            bool CanStep(Tile from, Tile to) => to.Coord != new GridCoord(2, 0);

            var reach = grid.GetReachable(new GridCoord(0, 0), maxSteps: 10, CanStep);

            Assert.IsTrue(reach.ContainsKey(new GridCoord(1, 0)));
            Assert.IsFalse(reach.ContainsKey(new GridCoord(2, 0)), "A política barra (2,0)...");
            Assert.IsFalse(reach.ContainsKey(new GridCoord(3, 0)), "...e (3,0) fica isolado atrás dele.");
        }

        [Test]
        public void GetReachable_FullOpenGrid_ReachesAllWithinSteps()
        {
            // 3x3 cheio (sem buraco): de (0,0), com 2 passos ortogonais, alcança o diamante de Manhattan ≤ 2.
            var coords = new List<GridCoord>();
            for (var x = 0; x < 3; x++)
            for (var z = 0; z < 3; z++)
                coords.Add(new GridCoord(x, z));
            var grid = BuildFromCoords(coords);

            var reach = grid.GetReachable(new GridCoord(0, 0), maxSteps: 2);

            // (2,2) tem distância de Manhattan 4 → fora do alcance 2.
            Assert.IsFalse(reach.ContainsKey(new GridCoord(2, 2)));
            // (2,0) e (0,2) estão a 2 passos.
            Assert.AreEqual(2, reach[new GridCoord(2, 0)]);
            Assert.AreEqual(2, reach[new GridCoord(0, 2)]);
            Assert.AreEqual(1, reach[new GridCoord(1, 0)]);
        }
    }
}
