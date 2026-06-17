using System.Collections.Generic;
using System.Text;
using Game.Core.Grid;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Bake editor-time do grid: coleta os <see cref="TileAuthoring"/> da cena ativa, infere a
    /// <see cref="GridCoord"/> de cada um a partir de <c>transform.position</c>, quantiza a altura em
    /// níveis inteiros, <b>valida</b> sobreposição/alinhamento e serializa o resultado num
    /// <see cref="GridData"/>. O runtime só carrega o asset — a validação vive aqui.
    /// </summary>
    public static class GridBaker
    {
        private const float HeightQuantizeTolerance = 0.01f;

        /// <summary>
        /// Bake da cena ativa. Se um <see cref="GridData"/> estiver selecionado no Project, reaproveita
        /// seu <c>cellSize</c>/<c>heightStep</c> e sobrescreve; senão abre um diálogo para criar um asset
        /// novo com os padrões.
        /// </summary>
        [MenuItem("GAMBIT/Grid/Bake Scene To GridData")]
        public static void BakeActiveScene()
        {
            var authorings = Object.FindObjectsByType<TileAuthoring>(FindObjectsSortMode.None);
            if (authorings.Length == 0)
            {
                EditorUtility.DisplayDialog("GAMBIT — Bake do Grid",
                    "Nenhum TileAuthoring na cena ativa. Posicione os tiles antes de bakear.", "OK");
                return;
            }

            var target = Selection.activeObject as GridData;
            var cellSize = target != null ? target.CellSize : 1f;
            var heightStep = target != null ? target.HeightStep : 0.5f;

            if (!TryBuildBakedTiles(authorings, cellSize, heightStep, out var baked, out var error))
            {
                EditorUtility.DisplayDialog("GAMBIT — Bake do Grid", error, "OK");
                return;
            }

            if (target == null)
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Salvar GridData", "GridData", "asset",
                    "Escolha onde salvar o asset baked do grid.");
                if (string.IsNullOrEmpty(path)) return;

                target = ScriptableObject.CreateInstance<GridData>();
                AssetDatabase.CreateAsset(target, path);
            }

            target.SetBakedContent(cellSize, heightStep, baked);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GridBaker] Bake concluído: {baked.Count} tiles em '{AssetDatabase.GetAssetPath(target)}' " +
                      $"(hash {target.ContentHash}).", target);
        }

        /// <summary>
        /// Converte os authorings em tiles baked, validando sobreposição e alinhamento. Pública para
        /// reuso pelo demo builder e por testes de editor.
        /// </summary>
        public static bool TryBuildBakedTiles(
            IReadOnlyList<TileAuthoring> authorings,
            float cellSize,
            float heightStep,
            out List<GridData.BakedTile> baked,
            out string error)
        {
            baked = new List<GridData.BakedTile>(authorings.Count);
            var seen = new Dictionary<GridCoord, TileAuthoring>();
            var overlaps = new StringBuilder();

            foreach (var authoring in authorings)
            {
                var pos = authoring.transform.position;
                var coord = new GridCoord(
                    Mathf.RoundToInt(pos.x / cellSize),
                    Mathf.RoundToInt(pos.z / cellSize));

                if (seen.TryGetValue(coord, out var other))
                {
                    overlaps.AppendLine($"  • {coord}: '{authoring.name}' e '{other.name}'");
                    continue;
                }

                seen.Add(coord, authoring);

                var rawLevel = pos.y / heightStep;
                var level = Mathf.RoundToInt(rawLevel);
                if (Mathf.Abs(rawLevel - level) > HeightQuantizeTolerance)
                    Debug.LogWarning($"[GridBaker] '{authoring.name}' em {coord} com altura fora da grade " +
                                     $"(y={pos.y}); quantizada para o nível {level}.", authoring);

                baked.Add(new GridData.BakedTile
                {
                    Coord = coord,
                    HeightLevel = level,
                    Type = authoring.Type,
                });
            }

            if (overlaps.Length > 0)
            {
                error = "Sobreposição de tiles na mesma coordenada (corrija o posicionamento):\n" + overlaps;
                baked = null;
                return false;
            }

            error = null;
            return true;
        }
    }
}
