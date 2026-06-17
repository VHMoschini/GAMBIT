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
    /// É uma alternativa de runtime aos cubos de autoria do <c>GridDemoBuilder</c>: aponte para o mesmo
    /// <see cref="GridData"/> (ou ao <see cref="GridRuntime"/>) e o tabuleiro aparece ao dar Play.
    /// </summary>
    public sealed class GridVisualizer : MonoBehaviour
    {
        [Tooltip("Dado baked a renderizar. Se vazio, tenta usar o GridData do GridRuntime no mesmo objeto.")]
        [SerializeField] private GridData gridData;

        [SerializeField] private GridRuntime gridRuntime;

        [Tooltip("Prefab opcional do tile. Se vazio, usa um cubo primitivo.")]
        [SerializeField] private GameObject tilePrefab;

        private void Start() => Build();

        public void Build()
        {
            var data = gridData != null ? gridData : (gridRuntime != null ? gridRuntime.Data : null);
            if (data == null)
            {
                Debug.LogError($"{nameof(GridVisualizer)}: nenhum {nameof(GridData)} para renderizar.", this);
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            foreach (var baked in data.Tiles)
            {
                var tile = tilePrefab != null
                    ? Instantiate(tilePrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);

                tile.transform.SetParent(transform, worldPositionStays: false);
                tile.name = $"Tile_{baked.Coord.X}_{baked.Coord.Z}";
                tile.transform.localPosition = new Vector3(
                    baked.Coord.X * data.CellSize,
                    baked.HeightLevel * data.HeightStep,
                    baked.Coord.Z * data.CellSize);
                tile.transform.localScale = new Vector3(data.CellSize * 0.95f, data.HeightStep, data.CellSize * 0.95f);

                if (tilePrefab == null && baked.Type != null)
                {
                    var renderer = tile.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = new Material(shader) { color = baked.Type.DebugColor };
                }
            }
        }
    }
}
