using System.Collections.Generic;
using Prototipo.Core;
using UnityEngine;

namespace Prototipo.Visual
{
    /// <summary>
    /// Constroi e gerencia a representacao visual 3D do tabuleiro em blocos estilo Kenney Prototype:
    /// texturas vibrantes puras (Off-White/Charcoal no centro, Roxo P1, Laranja P2, blocos WALL escuros nos obstaculos)
    /// e molduras 3D vazadas para highlights sem lavar a textura das casas.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float CellSize = 1.15f;
        public const float TileHeight = 0.52f;
        public const float BlockedHeight = 1.20f;

        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        private static readonly Color RimSelectedColor = new Color(1.00f, 0.88f, 0.20f);
        private static readonly Color RimMoveColor = new Color(0.14f, 0.94f, 1.00f);
        private static readonly Color RimAttackColor = new Color(1.00f, 0.22f, 0.26f);
        private static readonly Color RimSetupSpawnColor = new Color(0.95f, 0.98f, 1.00f);
        private static readonly Color RimHoverColor = new Color(1.00f, 1.00f, 1.00f);

        private sealed class TileVisual
        {
            public BoardCoord Coord;
            public TileKind Kind;
            public GameObject GameObject;
            public Renderer Renderer;
            public GameObject RimRoot;
            public Renderer[] RimRenderers;
            public Color BaseColor;
            public float TopY;
        }

        private readonly Dictionary<BoardCoord, TileVisual> _tilesByCoord = new Dictionary<BoardCoord, TileVisual>();
        private readonly Dictionary<GameObject, BoardCoord> _coordByObject = new Dictionary<GameObject, BoardCoord>();

        private readonly HashSet<BoardCoord> _moveHighlightSet = new HashSet<BoardCoord>();
        private readonly HashSet<BoardCoord> _attackHighlightSet = new HashSet<BoardCoord>();
        private readonly HashSet<BoardCoord> _setupSpawnHighlightSet = new HashSet<BoardCoord>();

        private PrototypeVisualTheme _theme;
        private Material _normalLightMaterial;
        private Material _normalDarkMaterial;
        private Material _spawnP1Material;
        private Material _spawnP2Material;
        private Material _blockedTileMaterial;
        private Material _arenaBaseMaterial;
        private Material _highlightRimMaterial;
        private GameObject _arenaBaseObject;
        private MaterialPropertyBlock _propertyBlock;
        private int _width;
        private int _height;

        private void Awake()
        {
            EnsureMaterials();
        }

        private void OnDestroy()
        {
            Clear();
            DestroyMaterials();
        }

        /// <summary>
        /// Define um <see cref="PrototypeVisualTheme"/> opcional (se nulo, usa os materiais Kenney padrao em Resources).
        /// </summary>
        public void SetTheme(PrototypeVisualTheme theme)
        {
            if (_theme == theme && _normalLightMaterial != null)
            {
                return;
            }

            _theme = theme;
            DestroyMaterials();
            EnsureMaterials();
        }

        /// <summary>
        /// Constroi o tabuleiro 3D a partir do grid logico.
        /// Celulas do tipo Hole nao geram geometria para formar buracos reais no tabuleiro.
        /// </summary>
        public void Build(BoardGrid grid)
        {
            Clear();
            EnsureMaterials();

            if (grid == null)
            {
                return;
            }

            _width = grid.Width;
            _height = grid.Height;

            BuildArenaUnderBase();

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

                if (kvp.Value.RimRoot != null)
                {
                    Destroy(kvp.Value.RimRoot);
                }
            }

            if (_arenaBaseObject != null)
            {
                Destroy(_arenaBaseObject);
                _arenaBaseObject = null;
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
        /// Atualiza os destaques usando molduras 3D nitidas no topo das casas, preservando as texturas Kenney intactas.
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
                Color tileTint = visual.BaseColor;
                bool showRim = false;
                Color rimColor = RimHoverColor;

                if (selected.HasValue && selected.Value == coord)
                {
                    showRim = true;
                    rimColor = RimSelectedColor;
                    tileTint = Color.Lerp(visual.BaseColor, RimSelectedColor, 0.16f);
                }
                else if (_attackHighlightSet.Contains(coord))
                {
                    showRim = true;
                    rimColor = RimAttackColor;
                    tileTint = Color.Lerp(visual.BaseColor, RimAttackColor, 0.34f);
                }
                else if (_moveHighlightSet.Contains(coord))
                {
                    showRim = true;
                    rimColor = RimMoveColor;
                    tileTint = Color.Lerp(visual.BaseColor, RimMoveColor, 0.24f);
                }
                else if (_setupSpawnHighlightSet.Contains(coord))
                {
                    showRim = true;
                    rimColor = RimSetupSpawnColor;
                }

                if (hovered.HasValue && hovered.Value == coord)
                {
                    if (!showRim)
                    {
                        showRim = true;
                        rimColor = RimHoverColor;
                    }

                    tileTint = Color.Lerp(tileTint, Color.white, 0.18f);
                }

                ApplyColor(visual.Renderer, tileTint);
                ApplyRimState(visual, showRim, rimColor);
            }
        }

        private void CreateTileVisual(BoardTile tile)
        {
            GameObject tileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileGo.name = "Tile_" + tile.Coord.X + "_" + tile.Coord.Z + "_" + tile.Kind;
            tileGo.transform.SetParent(transform, false);

            bool isBlocked = tile.Kind == TileKind.Blocked;
            float height = isBlocked ? BlockedHeight : TileHeight;
            float widthScale = isBlocked ? CellSize * 0.96f : CellSize * 0.985f;
            float bottomY = -TileHeight * 0.5f;
            float centerY = bottomY + height * 0.5f;
            float topY = bottomY + height;

            tileGo.transform.localPosition = new Vector3(tile.Coord.X * CellSize, centerY, tile.Coord.Z * CellSize);
            tileGo.transform.localScale = new Vector3(widthScale, height, widthScale);

            Renderer rend = tileGo.GetComponent<Renderer>();
            Material tileMat = GetMaterialForTile(tile.Coord, tile.Kind);
            if (rend != null && tileMat != null)
            {
                rend.sharedMaterial = tileMat;
            }

            GameObject rimRoot = null;
            Renderer[] rimRenderers = null;
            if (!isBlocked)
            {
                rimRoot = CreateHighlightFrame(
                    new Vector3(tile.Coord.X * CellSize, topY + 0.012f, tile.Coord.Z * CellSize),
                    widthScale * 0.96f,
                    out rimRenderers);
                rimRoot.SetActive(false);
            }

            TileVisual visual = new TileVisual
            {
                Coord = tile.Coord,
                Kind = tile.Kind,
                GameObject = tileGo,
                Renderer = rend,
                RimRoot = rimRoot,
                RimRenderers = rimRenderers,
                BaseColor = Color.white,
                TopY = topY
            };

            _tilesByCoord[tile.Coord] = visual;
            _coordByObject[tileGo] = tile.Coord;
        }

        private GameObject CreateHighlightFrame(Vector3 worldLocalPos, float frameSpan, out Renderer[] renderers)
        {
            GameObject root = new GameObject("HighlightFrame");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = worldLocalPos;

            const float barThickness = 0.085f;
            const float barHeight = 0.022f;
            float halfOffset = (frameSpan - barThickness) * 0.5f;

            renderers = new Renderer[4];
            renderers[0] = CreateRimBar(root.transform, "North", new Vector3(0f, 0f, halfOffset), new Vector3(frameSpan, barHeight, barThickness));
            renderers[1] = CreateRimBar(root.transform, "South", new Vector3(0f, 0f, -halfOffset), new Vector3(frameSpan, barHeight, barThickness));
            renderers[2] = CreateRimBar(root.transform, "East", new Vector3(halfOffset, 0f, 0f), new Vector3(barThickness, barHeight, frameSpan - barThickness * 2f));
            renderers[3] = CreateRimBar(root.transform, "West", new Vector3(-halfOffset, 0f, 0f), new Vector3(barThickness, barHeight, frameSpan - barThickness * 2f));
            return root;
        }

        private Renderer CreateRimBar(Transform parent, string barName, Vector3 localPos, Vector3 localScale)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = barName;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale = localScale;

            Collider col = bar.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            Renderer rend = bar.GetComponent<Renderer>();
            if (rend != null)
            {
                if (_highlightRimMaterial != null)
                {
                    rend.sharedMaterial = _highlightRimMaterial;
                }

                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return rend;
        }

        private void ApplyRimState(TileVisual visual, bool active, Color color)
        {
            if (visual.RimRoot == null)
            {
                return;
            }

            if (visual.RimRoot.activeSelf != active)
            {
                visual.RimRoot.SetActive(active);
            }

            if (active && visual.RimRenderers != null)
            {
                for (int i = 0; i < visual.RimRenderers.Length; i++)
                {
                    ApplyColor(visual.RimRenderers[i], color);
                }
            }
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

        private void BuildArenaUnderBase()
        {
            bool showBase = _theme == null || _theme.ShowArenaUnderBase;
            if (!showBase || _width <= 0 || _height <= 0)
            {
                return;
            }

            _arenaBaseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _arenaBaseObject.name = "ArenaUnderBase";
            _arenaBaseObject.transform.SetParent(transform, false);

            Collider col = _arenaBaseObject.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            const float baseThickness = 0.24f;
            float baseCenterY = -TileHeight * 0.5f - baseThickness * 0.5f - 0.02f;
            Vector3 center = GetBoardCenter();
            _arenaBaseObject.transform.localPosition = new Vector3(center.x, baseCenterY, center.z);
            _arenaBaseObject.transform.localScale = new Vector3(
                _width * CellSize + 0.70f,
                baseThickness,
                _height * CellSize + 0.70f);

            Renderer rend = _arenaBaseObject.GetComponent<Renderer>();
            if (rend != null)
            {
                if (_arenaBaseMaterial != null)
                {
                    rend.sharedMaterial = _arenaBaseMaterial;
                }

                Color tint = _theme != null ? _theme.ArenaBaseTint : new Color(0.72f, 0.75f, 0.82f, 1f);
                ApplyColor(rend, tint);
            }
        }

        private Material GetMaterialForTile(BoardCoord coord, TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Blocked:
                    return _blockedTileMaterial != null ? _blockedTileMaterial : _normalDarkMaterial;
                case TileKind.SpawnP1:
                    return _spawnP1Material != null ? _spawnP1Material : _normalLightMaterial;
                case TileKind.SpawnP2:
                    return _spawnP2Material != null ? _spawnP2Material : _normalLightMaterial;
                default:
                    bool isEven = ((coord.X + coord.Z) & 1) == 0;
                    return isEven ? _normalLightMaterial : _normalDarkMaterial;
            }
        }

        private void EnsureMaterials()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            if (_normalLightMaterial != null)
            {
                return;
            }

            Material preferredBase = _theme != null ? _theme.BaseLitMaterial : null;
            float? tileSmooth = _theme != null ? (float?)_theme.TileSmoothness : null;

            _normalLightMaterial = PrototypeMaterialFactory.CreateTexturedMaterial(
                "BoardTileNormalLight_Kenney",
                PrototypeMaterialFactory.ResTileNormal,
                _theme != null ? _theme.NormalTileTexture : null,
                _theme != null ? _theme.NormalTileTiling : Vector2.one,
                tileSmooth,
                preferredBase);

            _normalDarkMaterial = PrototypeMaterialFactory.CreateTexturedMaterial(
                "BoardTileNormalDark_Kenney",
                PrototypeMaterialFactory.ResTileNormalDark,
                _theme != null ? _theme.NormalTileDarkTexture : null,
                _theme != null ? _theme.NormalTileTiling : Vector2.one,
                tileSmooth,
                preferredBase);

            _spawnP1Material = PrototypeMaterialFactory.CreateTexturedMaterial(
                "BoardTileSpawnP1_Kenney",
                PrototypeMaterialFactory.ResTileSpawn,
                _theme != null ? _theme.SpawnTileTexture : null,
                _theme != null ? _theme.SpawnTileTiling : Vector2.one,
                tileSmooth,
                preferredBase);

            _spawnP2Material = PrototypeMaterialFactory.CreateTexturedMaterial(
                "BoardTileSpawnP2_Kenney",
                PrototypeMaterialFactory.ResTileSpawnP2,
                _theme != null ? _theme.SpawnP2TileTexture : null,
                _theme != null ? _theme.SpawnTileTiling : Vector2.one,
                tileSmooth,
                preferredBase);

            _blockedTileMaterial = PrototypeMaterialFactory.CreateTexturedMaterial(
                "BoardTileBlocked_Kenney",
                PrototypeMaterialFactory.ResTileBlocked,
                _theme != null ? _theme.BlockedTileTexture : null,
                _theme != null ? _theme.BlockedTileTiling : Vector2.one,
                tileSmooth,
                preferredBase);

            _arenaBaseMaterial = PrototypeMaterialFactory.CreateTexturedMaterial(
                "ArenaBase_Kenney",
                PrototypeMaterialFactory.ResArenaBase,
                _theme != null ? _theme.ArenaBaseTexture : null,
                _theme != null ? _theme.ArenaBaseTiling : new Vector2(7f, 7f),
                0.10f,
                preferredBase);

            _highlightRimMaterial = PrototypeMaterialFactory.CreateLitMaterial("BoardHighlightRim_URPLit", preferredBase);
        }

        private void DestroyMaterials()
        {
            if (_normalLightMaterial != null) { Destroy(_normalLightMaterial); _normalLightMaterial = null; }
            if (_normalDarkMaterial != null) { Destroy(_normalDarkMaterial); _normalDarkMaterial = null; }
            if (_spawnP1Material != null) { Destroy(_spawnP1Material); _spawnP1Material = null; }
            if (_spawnP2Material != null) { Destroy(_spawnP2Material); _spawnP2Material = null; }
            if (_blockedTileMaterial != null) { Destroy(_blockedTileMaterial); _blockedTileMaterial = null; }
            if (_arenaBaseMaterial != null) { Destroy(_arenaBaseMaterial); _arenaBaseMaterial = null; }
            if (_highlightRimMaterial != null) { Destroy(_highlightRimMaterial); _highlightRimMaterial = null; }
        }
    }
}
