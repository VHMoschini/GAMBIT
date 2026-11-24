using System.Collections.Generic;
using Prototipo.Core;
using UnityEngine;

namespace Prototipo.Visual
{
    /// <summary>
    /// Constroi e gerencia a representacao visual 3D do tabuleiro a partir do BoardGrid,
    /// incluindo casas normais, obstaculos altos, buracos reais e destaques via MaterialPropertyBlock.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float CellSize = 1.15f;
        public const float TileHeight = 0.25f;
        public const float BlockedHeight = 0.75f;

        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        private static readonly Color NormalLightColor = new Color(0.32f, 0.52f, 0.34f);
        private static readonly Color NormalDarkColor = new Color(0.22f, 0.38f, 0.25f);
        private static readonly Color SpawnP1Color = new Color(0.20f, 0.54f, 0.58f);
        private static readonly Color SpawnP2Color = new Color(0.66f, 0.38f, 0.48f);
        private static readonly Color BlockedColor = new Color(0.24f, 0.26f, 0.30f);

        private static readonly Color HighlightSelectedColor = new Color(1.00f, 0.90f, 0.35f);
        private static readonly Color HighlightMoveColor = new Color(0.12f, 0.85f, 0.96f);
        private static readonly Color HighlightAttackColor = new Color(0.95f, 0.20f, 0.22f);
        private static readonly Color HighlightSetupSpawnColor = new Color(0.45f, 0.90f, 0.36f);

        private sealed class TileVisual
        {
            public BoardCoord Coord;
            public TileKind Kind;
            public GameObject GameObject;
            public Renderer Renderer;
            public Color BaseColor;
            public float TopY;
        }

        private readonly Dictionary<BoardCoord, TileVisual> _tilesByCoord = new Dictionary<BoardCoord, TileVisual>();
        private readonly Dictionary<GameObject, BoardCoord> _coordByObject = new Dictionary<GameObject, BoardCoord>();

        private readonly HashSet<BoardCoord> _moveHighlightSet = new HashSet<BoardCoord>();
        private readonly HashSet<BoardCoord> _attackHighlightSet = new HashSet<BoardCoord>();
        private readonly HashSet<BoardCoord> _setupSpawnHighlightSet = new HashSet<BoardCoord>();

        private Material _sharedTileMaterial;
        private MaterialPropertyBlock _propertyBlock;
        private int _width;
        private int _height;

        private void Awake()
        {
            EnsureMaterial();
        }

        private void OnDestroy()
        {
            Clear();
            if (_sharedTileMaterial != null)
            {
                Destroy(_sharedTileMaterial);
                _sharedTileMaterial = null;
            }
        }

        /// <summary>
        /// Constroi o tabuleiro 3D a partir do grid logico.
        /// Celulas do tipo Hole nao geram geometria para formar buracos reais no tabuleiro.
        /// </summary>
        public void Build(BoardGrid grid)
        {
            Clear();
            EnsureMaterial();

            if (grid == null)
            {
                return;
            }

            _width = grid.Width;
            _height = grid.Height;

            IReadOnlyList<BoardTile> allTiles = grid.AllTiles;
            for (int i = 0; i < allTiles.Count; i++)
            {
                BoardTile tile = allTiles[i];
                if (tile.Kind == TileKind.Hole)
                {
                    continue;
                }

                CreateTileVisual(tile);
            }

            ApplyDefaultColors();
        }

        /// <summary>
        /// Remove todas as casas visuais do tabuleiro.
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<BoardCoord, TileVisual> kvp in _tilesByCoord)
            {
                if (kvp.Value.GameObject != null)
                {
                    Destroy(kvp.Value.GameObject);
                }
            }

            _tilesByCoord.Clear();
            _coordByObject.Clear();
            _moveHighlightSet.Clear();
            _attackHighlightSet.Clear();
            _setupSpawnHighlightSet.Clear();
            _width = 0;
            _height = 0;
        }

        /// <summary>
        /// Tenta resolver a coordenada logica a partir de um GameObject atingido por raycast.
        /// </summary>
        public bool TryGetCoord(GameObject go, out BoardCoord coord)
        {
            if (go == null)
            {
                coord = default;
                return false;
            }

            return _coordByObject.TryGetValue(go, out coord);
        }

        /// <summary>
        /// Retorna a posicao no topo da casa para posicionar pecas e indicadores.
        /// </summary>
        public Vector3 GetTileTopPosition(BoardCoord coord)
        {
            float topY = TileHeight * 0.5f;
            if (_tilesByCoord.TryGetValue(coord, out TileVisual visual))
            {
                topY = visual.TopY;
            }

            return new Vector3(coord.X * CellSize, topY, coord.Z * CellSize);
        }

        /// <summary>
        /// Retorna o centro geometrico do tabuleiro no espaco do mundo.
        /// </summary>
        public Vector3 GetBoardCenter()
        {
            if (_width <= 0 || _height <= 0)
            {
                return transform.position;
            }

            return new Vector3((_width - 1) * 0.5f * CellSize, 0f, (_height - 1) * 0.5f * CellSize);
        }

        /// <summary>
        /// Atualiza os destaques de selecao, movimento, ataque, zona de setup e hover usando MaterialPropertyBlock.
        /// </summary>
        public void SetHighlights(
            BoardCoord? selected,
            IEnumerable<BoardCoord> moveTargets,
            IEnumerable<BoardCoord> attackTargets,
            IEnumerable<BoardCoord> setupSpawnHighlight,
            BoardCoord? hovered)
        {
            _moveHighlightSet.Clear();
            if (moveTargets != null)
            {
                foreach (BoardCoord coord in moveTargets)
                {
                    _moveHighlightSet.Add(coord);
                }
            }

            _attackHighlightSet.Clear();
            if (attackTargets != null)
            {
                foreach (BoardCoord coord in attackTargets)
                {
                    _attackHighlightSet.Add(coord);
                }
            }

            _setupSpawnHighlightSet.Clear();
            if (setupSpawnHighlight != null)
            {
                foreach (BoardCoord coord in setupSpawnHighlight)
                {
                    _setupSpawnHighlightSet.Add(coord);
                }
            }

            foreach (KeyValuePair<BoardCoord, TileVisual> kvp in _tilesByCoord)
            {
                BoardCoord coord = kvp.Key;
                TileVisual visual = kvp.Value;
                Color targetColor = visual.BaseColor;

                if (selected.HasValue && selected.Value == coord)
                {
                    targetColor = HighlightSelectedColor;
                }
                else if (_attackHighlightSet.Contains(coord))
                {
                    targetColor = HighlightAttackColor;
                }
                else if (_moveHighlightSet.Contains(coord))
                {
                    targetColor = HighlightMoveColor;
                }
                else if (_setupSpawnHighlightSet.Contains(coord))
                {
                    targetColor = Color.Lerp(visual.BaseColor, HighlightSetupSpawnColor, 0.65f);
                }

                if (hovered.HasValue && hovered.Value == coord)
                {
                    targetColor = Color.Lerp(targetColor, Color.white, 0.26f);
                }

                ApplyColor(visual.Renderer, targetColor);
            }
        }

        private void CreateTileVisual(BoardTile tile)
        {
            GameObject tileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileGo.name = "Tile_" + tile.Coord.X + "_" + tile.Coord.Z + "_" + tile.Kind;
            tileGo.transform.SetParent(transform, false);

            bool isBlocked = tile.Kind == TileKind.Blocked;
            float height = isBlocked ? BlockedHeight : TileHeight;
            float widthScale = isBlocked ? CellSize * 0.92f : CellSize * 0.94f;
            float bottomY = -TileHeight * 0.5f;
            float centerY = bottomY + height * 0.5f;

            tileGo.transform.localPosition = new Vector3(tile.Coord.X * CellSize, centerY, tile.Coord.Z * CellSize);
            tileGo.transform.localScale = new Vector3(widthScale, height, widthScale);

            Renderer rend = tileGo.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _sharedTileMaterial;
            }

            TileVisual visual = new TileVisual
            {
                Coord = tile.Coord,
                Kind = tile.Kind,
                GameObject = tileGo,
                Renderer = rend,
                BaseColor = GetBaseColorForTile(tile.Coord, tile.Kind),
                TopY = bottomY + height
            };

            _tilesByCoord[tile.Coord] = visual;
            _coordByObject[tileGo] = tile.Coord;
        }

        private void ApplyDefaultColors()
        {
            foreach (KeyValuePair<BoardCoord, TileVisual> kvp in _tilesByCoord)
            {
                ApplyColor(kvp.Value.Renderer, kvp.Value.BaseColor);
            }
        }

        private void ApplyColor(Renderer rend, Color color)
        {
            if (rend == null)
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            rend.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorPropId, color);
            _propertyBlock.SetColor(ColorPropId, color);
            rend.SetPropertyBlock(_propertyBlock);
        }

        private void EnsureMaterial()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            if (_sharedTileMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _sharedTileMaterial = new Material(shader);
            _sharedTileMaterial.name = "BoardTile_URPLit";
        }

        private static Color GetBaseColorForTile(BoardCoord coord, TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Blocked:
                    return BlockedColor;
                case TileKind.SpawnP1:
                    return SpawnP1Color;
                case TileKind.SpawnP2:
                    return SpawnP2Color;
                default:
                    bool isEven = ((coord.X + coord.Z) & 1) == 0;
                    return isEven ? NormalLightColor : NormalDarkColor;
            }
        }
    }
}
