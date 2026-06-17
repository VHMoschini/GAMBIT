using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Grid
{
    /// <summary>
    /// Resultado <b>serializado</b> do bake (editor-time): a lista de tiles do tabuleiro + as âncoras de
    /// determinismo. O runtime apenas <i>carrega</i> este asset e constrói o <see cref="GridSystem"/> — sem
    /// <c>FindObjectsOfType</c> em <c>Awake</c> (ver arquitetura seção 3).
    ///
    /// <para><see cref="ContentHash"/> versiona o conteúdo: o log de partida grava esse hash no cabeçalho
    /// (Marco 5) para que um replay sobre mapa alterado vire <b>erro detectável</b>, não partida
    /// silenciosamente diferente.</para>
    /// </summary>
    public sealed class GridData : ScriptableObject
    {
        /// <summary>Uma célula baked — serializável pelo Unity.</summary>
        [Serializable]
        public struct BakedTile
        {
            public GridCoord Coord;
            public int HeightLevel;
            public TileType Type;
        }

        [Tooltip("Tamanho da célula no mundo, usado pelo bake (x/z → coord) e pela visualização.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Tamanho do nível de altura no mundo (position.y → nível inteiro).")]
        [SerializeField] private float heightStep = 0.5f;

        [Tooltip("Hash do conteúdo, recalculado a cada bake. Âncora de versão para replay.")]
        [SerializeField] private int contentHash;

        [SerializeField] private List<BakedTile> tiles = new List<BakedTile>();

        public float CellSize => cellSize;
        public float HeightStep => heightStep;
        public int ContentHash => contentHash;
        public IReadOnlyList<BakedTile> Tiles => tiles;

        /// <summary>Substitui o conteúdo baked e recalcula o hash de versão. Chamado pelo bake (editor).</summary>
        public void SetBakedContent(float cellSize, float heightStep, IEnumerable<BakedTile> bakedTiles)
        {
            this.cellSize = cellSize;
            this.heightStep = heightStep;
            tiles = new List<BakedTile>(bakedTiles);
            contentHash = ComputeContentHash(tiles);
        }

        /// <summary>
        /// Constrói o <see cref="GridSystem"/> POCO a partir do dado baked. Lança em sobreposição
        /// (mesma coordenada duas vezes) — a mesma invariante que o bake já valida.
        /// </summary>
        public GridSystem CreateGridSystem()
        {
            var grid = new GridSystem();
            foreach (var baked in tiles)
                grid.AddTile(new Tile(baked.Coord, baked.HeightLevel, baked.Type));
            return grid;
        }

        /// <summary>
        /// Hash determinístico do conteúdo: independe da ordem da lista (combina por XOR sobre um hash
        /// por tile). O nome do <see cref="TileType"/> entra no hash para que trocar o terreno de uma
        /// célula altere a versão.
        /// </summary>
        public static int ComputeContentHash(IReadOnlyList<BakedTile> tiles)
        {
            var hash = 17;
            foreach (var t in tiles)
            {
                var typeId = t.Type != null ? t.Type.name.GetHashCode() : 0;
                var tileHash = t.Coord.GetHashCode();
                tileHash = unchecked(tileHash * 31 + t.HeightLevel);
                tileHash = unchecked(tileHash * 31 + typeId);
                hash ^= tileHash; // XOR: independente da ordem de iteração
            }

            return hash;
        }
    }
}
