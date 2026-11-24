using UnityEngine;

namespace Prototipo.Visual
{
    /// <summary>
    /// Tema visual editavel no Inspector para trocar facilmente as texturas Kenney Prototype,
    /// tiling e acabamento (smoothness) do tabuleiro, obstaculos, base e pecas sem mexer em codigo.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualTheme_Prototipo", menuName = "GAMBIT/Prototipo/Visual Theme", order = 2)]
    public sealed class PrototypeVisualTheme : ScriptableObject
    {
        [Header("Material Base URP (opcional — fallback automatico em Resources/PrototipoLit)")]
        [SerializeField] private Material baseLitMaterial;

        [Header("Texturas do Tabuleiro (Kenney Prototype)")]
        [SerializeField] private Texture2D normalTileTexture;
        [SerializeField] private Texture2D normalTileDarkTexture;
        [SerializeField] private Vector2 normalTileTiling = Vector2.one;

        [SerializeField] private Texture2D spawnTileTexture;
        [SerializeField] private Texture2D spawnP2TileTexture;
        [SerializeField] private Vector2 spawnTileTiling = Vector2.one;

        [SerializeField] private Texture2D blockedTileTexture;
        [SerializeField] private Vector2 blockedTileTiling = Vector2.one;

        [Header("Base / Moldura sob a Arena (valoriza os buracos)")]
        [SerializeField] private bool showArenaUnderBase = true;
        [SerializeField] private Texture2D arenaBaseTexture;
        [SerializeField] private Vector2 arenaBaseTiling = new Vector2(7f, 7f);
        [SerializeField] private Color arenaBaseTint = new Color(0.72f, 0.75f, 0.82f, 1f);

        [Header("Texturas das Pecas e Capsulas")]
        [SerializeField] private Texture2D pieceBodyTexture;
        [SerializeField] private Vector2 pieceBodyTiling = new Vector2(2f, 1f);

        [SerializeField] private Texture2D capsuleShellTexture;
        [SerializeField] private Vector2 capsuleShellTiling = new Vector2(2f, 1f);

        [Header("Acabamento URP Lit")]
        [Range(0f, 1f)] [SerializeField] private float tileSmoothness = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float pieceSmoothness = 0.22f;

        public Material BaseLitMaterial => baseLitMaterial;
        public Texture2D NormalTileTexture => normalTileTexture;
        public Texture2D NormalTileDarkTexture => normalTileDarkTexture;
        public Vector2 NormalTileTiling => normalTileTiling;
        public Texture2D SpawnTileTexture => spawnTileTexture;
        public Texture2D SpawnP2TileTexture => spawnP2TileTexture;
        public Vector2 SpawnTileTiling => spawnTileTiling;
        public Texture2D BlockedTileTexture => blockedTileTexture;
        public Vector2 BlockedTileTiling => blockedTileTiling;
        public bool ShowArenaUnderBase => showArenaUnderBase;
        public Texture2D ArenaBaseTexture => arenaBaseTexture;
        public Vector2 ArenaBaseTiling => arenaBaseTiling;
        public Color ArenaBaseTint => arenaBaseTint;
        public Texture2D PieceBodyTexture => pieceBodyTexture;
        public Vector2 PieceBodyTiling => pieceBodyTiling;
        public Texture2D CapsuleShellTexture => capsuleShellTexture;
        public Vector2 CapsuleShellTiling => capsuleShellTiling;
        public float TileSmoothness => tileSmoothness;
        public float PieceSmoothness => pieceSmoothness;

        /// <summary>
        /// Preenche o tema com as texturas vibrantes padrao da Kenney (chamado pelo setup de Editor).
        /// </summary>
        public void ConfigureDefaults(
            Material litMaterial,
            Texture2D normalLightTex,
            Texture2D normalDarkTex,
            Texture2D spawnP1Tex,
            Texture2D spawnP2Tex,
            Texture2D blockedTex,
            Texture2D arenaTex,
            Texture2D pieceTex,
            Texture2D capsuleTex)
        {
            baseLitMaterial = litMaterial;
            normalTileTexture = normalLightTex;
            normalTileDarkTexture = normalDarkTex;
            normalTileTiling = Vector2.one;
            spawnTileTexture = spawnP1Tex;
            spawnP2TileTexture = spawnP2Tex;
            spawnTileTiling = Vector2.one;
            blockedTileTexture = blockedTex;
            blockedTileTiling = Vector2.one;
            showArenaUnderBase = true;
            arenaBaseTexture = arenaTex;
            arenaBaseTiling = new Vector2(7f, 7f);
            arenaBaseTint = new Color(0.72f, 0.75f, 0.82f, 1f);
            pieceBodyTexture = pieceTex;
            pieceBodyTiling = new Vector2(2f, 1f);
            capsuleShellTexture = capsuleTex;
            capsuleShellTiling = new Vector2(2f, 1f);
            tileSmoothness = 0.12f;
            pieceSmoothness = 0.22f;
        }
    }
}
