using System;
using System.Collections.Generic;

namespace Prototipo.Core
{
    /// <summary>
    /// Configuração imutável de partida usada pelo <see cref="MatchEngine"/> (POCO puro para testes e runtime).
    /// </summary>
    public sealed class MatchConfigData
    {
        public int BoardWidth { get; }
        public int BoardHeight { get; }
        public int CapsulesPerPlayer { get; }
        public int ActionPointsPerTurn { get; }
        public IReadOnlyList<PieceSpec> Catalog { get; }

        public MatchConfigData(
            int boardWidth,
            int boardHeight,
            int capsulesPerPlayer,
            int actionPointsPerTurn,
            IEnumerable<PieceSpec> catalog)
        {
            BoardWidth = Math.Max(3, boardWidth);
            BoardHeight = Math.Max(5, boardHeight);
            CapsulesPerPlayer = Math.Max(1, capsulesPerPlayer);
            ActionPointsPerTurn = Math.Max(1, actionPointsPerTurn);

            var list = new List<PieceSpec>();
            if (catalog != null)
            {
                foreach (PieceSpec spec in catalog)
                {
                    if (spec != null)
                    {
                        list.Add(spec);
                    }
                }
            }

            if (list.Count == 0)
            {
                list.AddRange(DefaultPieceCatalog.CreateDefaultSpecs());
            }

            Catalog = list.AsReadOnly();
        }

        public PieceSpec FindSpec(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < Catalog.Count; i++)
            {
                if (string.Equals(Catalog[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return Catalog[i];
                }
            }

            return null;
        }

        public static MatchConfigData CreateDefault()
        {
            return new MatchConfigData(
                boardWidth: 7,
                boardHeight: 7,
                capsulesPerPlayer: 4,
                actionPointsPerTurn: 6,
                catalog: DefaultPieceCatalog.CreateDefaultSpecs());
        }
    }
}
