using Game.Core.Grid;
using Game.Core.Pieces;
using Game.Core.Pieces.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Cria, num clique, os assets de demonstração do Marco 2: uma <see cref="MoveActionDefinition"/> e
    /// uma <see cref="PieceDefinition"/> ("Runner") com alguns <see cref="MovePattern"/> visíveis no
    /// tabuleiro de demo. Atribua a PieceDefinition no <c>MatchController</c> da cena para jogar o slice.
    /// Assets ficam sob <c>Assets/PieceDemo</c>.
    /// </summary>
    public static class PieceDemoBuilder
    {
        private const string DemoFolder = "Assets/PieceDemo";

        [MenuItem("GAMBIT/Pieces/Create Demo Piece")]
        public static void CreateDemoPiece()
        {
            EnsureFolder();

            var move = GetOrCreate<MoveActionDefinition>($"{DemoFolder}/Move.asset", () => MoveActionDefinition.Create("Move"));
            var definition = GetOrCreate($"{DemoFolder}/Runner.asset", () => PieceDefinition.Create(
                "Runner",
                new[]
                {
                    new MovePattern("1 frente", new GridCoord(0, 1)),
                    new MovePattern("2 frente", new GridCoord(0, 1), new GridCoord(0, 2)),
                    new MovePattern("2 direita", new GridCoord(1, 0), new GridCoord(2, 0)),
                    new MovePattern("2 esquerda", new GridCoord(-1, 0), new GridCoord(-2, 0)),
                    new MovePattern("1 atrás", new GridCoord(0, -1)),
                },
                new ActionDefinition[] { move }));

            Selection.activeObject = definition;
            Debug.Log($"[PieceDemoBuilder] Assets de demo criados em {DemoFolder}. " +
                      "Atribua 'Runner' ao campo Piece Definition do MatchController na cena.");
        }

        private static T GetOrCreate<T>(string path, System.Func<T> factory) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = factory();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(DemoFolder))
                AssetDatabase.CreateFolder("Assets", "PieceDemo");
        }
    }
}
