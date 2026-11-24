using System.Collections.Generic;
using UnityEngine;

namespace Prototipo.Core
{
    /// <summary>
    /// Asset de configuração geral da partida do protótipo.
    /// </summary>
    [CreateAssetMenu(fileName = "PrototypeConfig", menuName = "GAMBIT/Prototipo/Config")]
    public sealed class PrototypeConfig : ScriptableObject
    {
        [SerializeField] private int boardWidth = 7;
        [SerializeField] private int boardHeight = 7;
        [SerializeField] private int capsulesPerPlayer = 4;
        [SerializeField] private int actionPointsPerTurn = 6;
        [SerializeField] private List<PieceBlueprint> pieceBlueprints = new List<PieceBlueprint>();

        public int BoardWidth => boardWidth;
        public int BoardHeight => boardHeight;
        public int CapsulesPerPlayer => capsulesPerPlayer;
        public int ActionPointsPerTurn => actionPointsPerTurn;
        public IReadOnlyList<PieceBlueprint> PieceBlueprints => pieceBlueprints;

        public MatchConfigData ToConfigData()
        {
            var specs = new List<PieceSpec>();
            if (pieceBlueprints != null)
            {
                for (int i = 0; i < pieceBlueprints.Count; i++)
                {
                    PieceBlueprint blueprint = pieceBlueprints[i];
                    if (blueprint != null)
                    {
                        specs.Add(blueprint.ToSpec());
                    }
                }
            }

            if (specs.Count == 0)
            {
                specs = DefaultPieceCatalog.CreateDefaultSpecs();
            }

            return new MatchConfigData(
                boardWidth,
                boardHeight,
                capsulesPerPlayer,
                actionPointsPerTurn,
                specs);
        }

        public void ConfigureDefaults(List<PieceBlueprint> blueprints)
        {
            boardWidth = 7;
            boardHeight = 7;
            capsulesPerPlayer = 4;
            actionPointsPerTurn = 6;
            pieceBlueprints = blueprints != null
                ? new List<PieceBlueprint>(blueprints)
                : new List<PieceBlueprint>();
        }
    }
}
