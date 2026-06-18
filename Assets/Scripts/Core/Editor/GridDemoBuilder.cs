using System.Collections.Generic;
using Game.Core.Common;
using Game.Core.Grid;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Gera, num clique, um tabuleiro irregular de demonstração (com buraco) na cena ativa: tiles de
    /// autoria (<see cref="TileAuthoring"/>) já visíveis como cubos coloridos pelo <see cref="TileType"/>.
    /// Serve ao entregável do Marco 1 — abrir a cena e ver o tabuleiro — e dá entrada pronta para o
    /// <see cref="GridBaker"/>. Os assets de <see cref="TileType"/> são criados sob <c>Assets/GridDemo</c>.
    /// </summary>
    public static class GridDemoBuilder
    {
        private const string DemoFolder = "Assets/GridDemo";
        private const float CellSize = 1f;
        private const float HeightStep = 0.5f;
        private const int Size = 7;

        // Buraco no meio do tabuleiro — tabuleiro irregular (peças têm de contorná-lo).
        private static readonly HashSet<GridCoord> Holes = new HashSet<GridCoord>
        {
            new GridCoord(3, 3),
            new GridCoord(3, 4),
            new GridCoord(4, 3),
        };

        [MenuItem("GAMBIT/Grid/Create Demo Grid")]
        public static void CreateDemoGrid()
        {
            EnsureFolder();
            var grass = GetOrCreateTileType("Grass", new Color(0.45f, 0.7f, 0.35f), blocks: false);
            var rock = GetOrCreateTileType("Rock", new Color(0.5f, 0.5f, 0.55f), blocks: true);

            var root = SceneObjectFactory.Create("DemoGrid", parent: null, owner: typeof(GridDemoBuilder));
            Undo.RegisterCreatedObjectUndo(root, "Create Demo Grid");

            for (var x = 0; x < Size; x++)
            for (var z = 0; z < Size; z++)
            {
                var coord = new GridCoord(x, z);
                if (Holes.Contains(coord)) continue;

                // Algumas pedras intransponíveis nas bordas para enriquecer a topologia.
                var isRock = (x == 6 && z == 1) || (x == 1 && z == 5);
                var type = isRock ? rock : grass;
                var level = isRock ? 1 : 0;

                var tile = SceneObjectFactory.CreatePrimitive(
                    PrimitiveType.Cube, $"Tile_{x}_{z}", root.transform, owner: typeof(GridDemoBuilder));
                tile.transform.position = new Vector3(x * CellSize, level * HeightStep, z * CellSize);
                tile.transform.localScale = new Vector3(CellSize * 0.95f, HeightStep, CellSize * 0.95f);

                var authoring = tile.AddComponent<TileAuthoring>();
                authoring.SetType(type);
                ApplyColor(tile, type.DebugColor);
            }

            Selection.activeGameObject = root;
            Debug.Log($"[GridDemoBuilder] Tabuleiro {Size}x{Size} criado com {Holes.Count} células de buraco. " +
                      "Use 'GAMBIT/Grid/Bake Scene To GridData' para gerar o GridData.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(DemoFolder))
                AssetDatabase.CreateFolder("Assets", "GridDemo");
        }

        private static TileType GetOrCreateTileType(string typeName, Color color, bool blocks)
        {
            var path = $"{DemoFolder}/{typeName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TileType>(path);
            if (existing != null) return existing;

            var type = ScriptableObject.CreateInstance<TileType>();
            type.name = typeName;
            // Os campos são privados serializados; setamos via SerializedObject para não vazar setters no runtime.
            var so = new SerializedObject(type);
            so.FindProperty("displayName").stringValue = typeName;
            so.FindProperty("debugColor").colorValue = color;
            so.FindProperty("blocksMovement").boolValue = blocks;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(type, path);
            return type;
        }

        private static void ApplyColor(GameObject tile, Color color)
        {
            var renderer = tile.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            // Material de instância só para a visualização de autoria; o core não sabe disso.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            renderer.sharedMaterial = material;
        }
    }
}
