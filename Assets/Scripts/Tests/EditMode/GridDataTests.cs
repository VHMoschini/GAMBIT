using System.Collections.Generic;
using Game.Core.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class GridDataTests
    {
        private static List<GridData.BakedTile> SampleTiles() => new List<GridData.BakedTile>
        {
            new GridData.BakedTile { Coord = new GridCoord(0, 0), HeightLevel = 0, Type = null },
            new GridData.BakedTile { Coord = new GridCoord(1, 0), HeightLevel = 1, Type = null },
            new GridData.BakedTile { Coord = new GridCoord(0, 1), HeightLevel = 0, Type = null },
        };

        [Test]
        public void CreateGridSystem_RoundTripsBakedTiles()
        {
            var data = ScriptableObject.CreateInstance<GridData>();
            data.SetBakedContent(1f, 0.5f, SampleTiles());

            var grid = data.CreateGridSystem();

            Assert.AreEqual(3, grid.Count);
            Assert.IsTrue(grid.HasTile(new GridCoord(1, 0)));
            Assert.AreEqual(1, grid.GetTile(new GridCoord(1, 0)).HeightLevel);
            Assert.IsFalse(grid.HasTile(new GridCoord(2, 2)));

            Object.DestroyImmediate(data);
        }

        [Test]
        public void ContentHash_IsIndependentOfTileOrder()
        {
            var forward = SampleTiles();
            var reversed = new List<GridData.BakedTile>(forward);
            reversed.Reverse();

            // Hash por XOR → mesma versão independente da ordem em que o bake coletou os tiles.
            Assert.AreEqual(
                GridData.ComputeContentHash(forward),
                GridData.ComputeContentHash(reversed));
        }

        [Test]
        public void ContentHash_ChangesWhenHeightChanges()
        {
            var a = SampleTiles();
            var b = SampleTiles();
            b[0] = new GridData.BakedTile { Coord = new GridCoord(0, 0), HeightLevel = 5, Type = null };

            Assert.AreNotEqual(
                GridData.ComputeContentHash(a),
                GridData.ComputeContentHash(b));
        }
    }
}
