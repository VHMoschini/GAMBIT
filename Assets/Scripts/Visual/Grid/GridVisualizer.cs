using System.Collections.Generic;
using Game.Core.Common;
using Game.Core.Grid;
using UnityEngine;

namespace Game.Visual.Grid
{
    /// <summary>
    /// Renderiza o tabuleiro a partir do <see cref="GridData"/> baked: instancia um placeholder por tile,
    /// posicionado pela <see cref="GridCoord"/>/altura e colorido pelo <see cref="TileType.DebugColor"/>.
    /// Demonstra o princípio "core fala, visual ouve" — o visual <b>lê</b> o dado do core e desenha;
    /// o core nunca sabe que existe render.
    ///
    /// É uma alternativa de runtime aos cubos de autoria do <c>GridDemoBuilder</c>: aponte para o
    /// <see cref="GridData"/> baked e o tabuleiro aparece ao dar Play.
    ///
    /// <para><b>Referência de autoria (editor):</b> com <c>drawGizmos</c> ligado, desenha a grid como
    /// gizmos no Scene view <b>fora do Play</b> — sem instanciar objetos — para o artista compor o
    /// cenário tendo a footprint lógica do tabuleiro como referência. Requer um <see cref="GridData"/>
    /// baked atribuído (rode o bake após autorar/mover tiles para atualizar a referência).</para>
    /// </summary>
    public sealed class GridVisualizer : MonoBehaviour
    {
        [Tooltip("Dado baked a renderizar (asset produzido pelo GridBaker, menu GAMBIT/Grid).")]
        [SerializeField] private GridData gridData;

        [Tooltip("Prefab opcional do tile. Se vazio, usa um cubo primitivo.")]
        [SerializeField] private GameObject tilePrefab;

        [Header("Referência de autoria (gizmos no Scene view, fora do Play)")]
        [Tooltip("Desenha a grid como gizmos no editor, sem criar objetos na cena.")]
        [SerializeField] private bool drawGizmos = true;

        [Tooltip("Preenche cada tile com a DebugColor translúcida, além do contorno.")]
        [SerializeField] private bool drawFilledGizmos = true;

        [Tooltip("Escreve a coordenada (X,Z) em cada tile.")]
        [SerializeField] private bool drawCoordLabels;

        [Range(0f, 1f)]
        [Tooltip("Opacidade do preenchimento dos gizmos.")]
        [SerializeField] private float gizmoFillAlpha = 0.25f;

        private readonly Dictionary<GridCoord, GameObject> _tileObjects = new Dictionary<GridCoord, GameObject>();
        private readonly Dictionary<GameObject, GridCoord> _coordByObject = new Dictionary<GameObject, GridCoord>();
        private bool _built;

        /// <summary>Tile renderizado por coordenada (disponível após o build). Leitura do controller/highlighter.</summary>
        public IReadOnlyDictionary<GridCoord, GameObject> TileObjects => _tileObjects;

        private void Start() => EnsureBuilt();

        /// <summary>Constrói o tabuleiro uma única vez (idempotente). Chamado por quem precisar dos tiles no Start.</summary>
        public void EnsureBuilt()
        {
            if (_built) return;
            Build();
        }

        private void Build()
        {
            if (gridData == null)
            {
                Debug.LogError($"{nameof(GridVisualizer)}: nenhum {nameof(GridData)} para renderizar.", this);
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            foreach (var baked in gridData.Tiles)
            {
                var baseName = $"Tile_{baked.Coord.X}_{baked.Coord.Z}";
                var tile = tilePrefab != null
                    ? SceneObjectFactory.Instantiate(tilePrefab, baseName, transform, this)
                    : SceneObjectFactory.CreatePrimitive(PrimitiveType.Cube, baseName, transform, this);

                tile.transform.localPosition = new Vector3(
                    baked.Coord.X * gridData.CellSize,
                    baked.HeightLevel * gridData.HeightStep,
                    baked.Coord.Z * gridData.CellSize);
                tile.transform.localScale = new Vector3(gridData.CellSize * 0.95f, gridData.HeightStep, gridData.CellSize * 0.95f);

                if (tilePrefab == null && baked.Type != null)
                {
                    var renderer = tile.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = new Material(shader) { color = baked.Type.DebugColor };
                }

                _tileObjects[baked.Coord] = tile;
                _coordByObject[tile] = baked.Coord;
            }

            _built = true;
        }

        /// <summary>Coordenada do tile renderizado de um GameObject (ex: alvo de um raycast). Falha se não for um tile.</summary>
        public bool TryGetCoord(GameObject tileObject, out GridCoord coord)
            => _coordByObject.TryGetValue(tileObject, out coord);

        /// <summary>Posição de mundo logo acima da célula — para apoiar/animar uma peça sobre o tile.</summary>
        public Vector3 TopOf(GridCoord coord)
        {
            if (_tileObjects.TryGetValue(coord, out var tile))
                return tile.transform.position + Vector3.up * (gridData.HeightStep * 0.5f + 0.3f);

            // Fallback (coordenada sem tile renderizado): calcula pela footprint lógica.
            var local = new Vector3(coord.X * gridData.CellSize, 0f, coord.Z * gridData.CellSize);
            return transform.TransformPoint(local) + Vector3.up * 0.3f;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || gridData == null) return;

            var matrixBackup = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix; // respeita posição/rotação/escala do objeto

            foreach (var baked in gridData.Tiles)
            {
                var center = new Vector3(
                    baked.Coord.X * gridData.CellSize,
                    baked.HeightLevel * gridData.HeightStep,
                    baked.Coord.Z * gridData.CellSize);
                var size = new Vector3(gridData.CellSize * 0.95f, gridData.HeightStep, gridData.CellSize * 0.95f);
                var color = baked.Type != null ? baked.Type.DebugColor : Color.gray;

                if (drawFilledGizmos)
                {
                    Gizmos.color = new Color(color.r, color.g, color.b, gizmoFillAlpha);
                    Gizmos.DrawCube(center, size);
                }

                Gizmos.color = color;
                Gizmos.DrawWireCube(center, size);
            }

            Gizmos.matrix = matrixBackup;

#if UNITY_EDITOR
            if (!drawCoordLabels) return;
            foreach (var baked in gridData.Tiles)
            {
                var local = new Vector3(
                    baked.Coord.X * gridData.CellSize,
                    baked.HeightLevel * gridData.HeightStep + gridData.HeightStep * 0.5f,
                    baked.Coord.Z * gridData.CellSize);
                UnityEditor.Handles.Label(transform.TransformPoint(local), $"{baked.Coord.X},{baked.Coord.Z}");
            }
#endif
        }
    }
}
