using Game.Core.Grid;
using Game.Core.Pieces;
using Game.Core.Pieces.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Cria, num clique, os assets de demonstração do slice: as ações (<see cref="MoveActionDefinition"/> +
    /// <see cref="AttackActionDefinition"/>) e duas <see cref="PieceDefinition"/> — o "Runner" (peça do
    /// Player1, move + ataca) e o "Target" (peça do Player2, alvo que morre ao ser atingido). Atribua
    /// Runner/Target aos campos do <c>MatchController</c> da cena. Assets ficam sob <c>Assets/PieceDemo</c>.
    /// </summary>
    public static class PieceDemoBuilder
    {
        private const string DemoFolder = "Assets/PieceDemo";

        [MenuItem("GAMBIT/Pieces/Create Demo Piece")]
        public static void CreateDemoPiece()
        {
            EnsureFolder();

            var move = GetOrCreate<MoveActionDefinition>($"{DemoFolder}/Move.asset", () => MoveActionDefinition.Create("Move"));
            var attack = GetOrCreate<AttackActionDefinition>($"{DemoFolder}/Attack.asset", () => AttackActionDefinition.Create(range: 1, displayName: "Attack"));

            var runner = GetOrCreate($"{DemoFolder}/Runner.asset", () => PieceDefinition.Create(
                "Runner",
                new[]
                {
                    new MovePattern("1 frente", new GridCoord(0, 1)),
                    new MovePattern("2 frente", new GridCoord(0, 1), new GridCoord(0, 2)),
                    new MovePattern("2 direita", new GridCoord(1, 0), new GridCoord(2, 0)),
                    new MovePattern("2 esquerda", new GridCoord(-1, 0), new GridCoord(-2, 0)),
                    new MovePattern("1 atrás", new GridCoord(0, -1)),
                },
                new ActionDefinition[] { move, attack },
                attack: 3, defense: 0, maxHP: 5));

            var target = GetOrCreate($"{DemoFolder}/Target.asset", () => PieceDefinition.Create(
                "Target",
                new[] { new MovePattern("1 frente", new GridCoord(0, 1)) },
                new ActionDefinition[] { move },
                attack: 1, defense: 1, maxHP: 2)); // Runner (3) - Defense (1) = 2 de dano → morre num golpe.

            Selection.activeObject = runner;
            Debug.Log($"[PieceDemoBuilder] Assets de demo criados em {DemoFolder}. " +
                      "Atribua 'Runner' ao Piece Definition e 'Target' ao Enemy Definition do MatchController.");
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
