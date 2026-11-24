using UnityEngine;
using UnityEngine.Rendering;

namespace Prototipo.Visual
{
    /// <summary>
    /// Cria materiais URP Lit (lisos ou texturizados com Kenney Prototype Textures) de forma segura
    /// tanto no Editor quanto em Standalone Builds. Prioriza os materiais pré-configurados em
    /// <c>Assets/prototipo/Resources/</c> e aplica overrides de um <see cref="PrototypeVisualTheme"/> se atribuido.
    /// </summary>
    internal static class PrototypeMaterialFactory
    {
        public const string ResLit = "PrototipoLit";
        public const string ResTileNormal = "PrototipoTileNormal";
        public const string ResTileNormalDark = "PrototipoTileNormalDark";
        public const string ResTileSpawn = "PrototipoTileSpawn";
        public const string ResTileSpawnP2 = "PrototipoTileSpawnP2";
        public const string ResTileBlocked = "PrototipoTileBlocked";
        public const string ResArenaBase = "PrototipoArenaBase";
        public const string ResPieceBody = "PrototipoPieceBody";
        public const string ResCapsuleShell = "PrototipoCapsuleShell";

        private static readonly int BaseMapPropId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexPropId = Shader.PropertyToID("_MainTex");
        private static readonly int SmoothnessPropId = Shader.PropertyToID("_Smoothness");

        public static Material CreateLitMaterial(string materialName, Material preferredBase = null)
        {
            if (preferredBase != null && preferredBase.shader != null && preferredBase.shader.isSupported)
            {
                Material fromPreferred = new Material(preferredBase);
                fromPreferred.name = materialName;
                return fromPreferred;
            }

            Material resourceMat = Resources.Load<Material>(ResLit);
            if (resourceMat != null && resourceMat.shader != null && resourceMat.shader.isSupported)
            {
                Material fromResource = new Material(resourceMat);
                fromResource.name = materialName;
                return fromResource;
            }

            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (urpShader != null)
            {
                Material fromUrpShader = new Material(urpShader);
                fromUrpShader.name = materialName;
                return fromUrpShader;
            }

            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
            {
                Material fromRp = new Material(rp.defaultMaterial);
                fromRp.name = materialName;
                return fromRp;
            }

            Shader fallbackShader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (fallbackShader != null)
            {
                Material fromFallback = new Material(fallbackShader);
                fromFallback.name = materialName;
                return fromFallback;
            }

            Debug.LogWarning("[PrototypeMaterialFactory] Nenhum shader/material disponivel para '" + materialName + "'.");
            return null;
        }

        /// <summary>
        /// Cria uma instancia de material texturizado:
        /// se <paramref name="overrideTexture"/> for informado (via PrototypeVisualTheme), aplica sobre o material base URP;
        /// senao carrega o material Kenney ja pronto em <c>Resources/<paramref name="resourceMaterialName"/></c>.
        /// </summary>
        public static Material CreateTexturedMaterial(
            string materialName,
            string resourceMaterialName,
            Texture2D overrideTexture,
            Vector2 overrideTiling,
            float? overrideSmoothness = null,
            Material preferredBase = null)
        {
            Material mat;
            if (overrideTexture != null)
            {
                mat = CreateLitMaterial(materialName, preferredBase);
                if (mat != null)
                {
                    ApplyTexture(mat, overrideTexture, overrideTiling);
                }
            }
            else
            {
                Material prebuilt = !string.IsNullOrEmpty(resourceMaterialName)
                    ? Resources.Load<Material>(resourceMaterialName)
                    : null;

                if (prebuilt != null && prebuilt.shader != null && prebuilt.shader.isSupported)
                {
                    mat = new Material(prebuilt);
                    mat.name = materialName;
                }
                else
                {
                    mat = CreateLitMaterial(materialName, preferredBase);
                }
            }

            if (mat != null && overrideSmoothness.HasValue)
            {
                mat.SetFloat(SmoothnessPropId, Mathf.Clamp01(overrideSmoothness.Value));
            }

            if (mat != null)
            {
                EnhanceTextureSharpness(mat);
            }

            return mat;
        }

        private static void EnhanceTextureSharpness(Material mat)
        {
            Texture tex = mat.HasProperty(BaseMapPropId) ? mat.GetTexture(BaseMapPropId) : null;
            if (tex == null && mat.HasProperty(MainTexPropId))
            {
                tex = mat.GetTexture(MainTexPropId);
            }

            if (tex != null)
            {
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 16;
            }
        }

        private static void ApplyTexture(Material mat, Texture2D texture, Vector2 tiling)
        {
            Vector2 safeTiling = new Vector2(
                Mathf.Approximately(tiling.x, 0f) ? 1f : tiling.x,
                Mathf.Approximately(tiling.y, 0f) ? 1f : tiling.y);

            if (mat.HasProperty(BaseMapPropId))
            {
                mat.SetTexture(BaseMapPropId, texture);
                mat.SetTextureScale(BaseMapPropId, safeTiling);
            }

            if (mat.HasProperty(MainTexPropId))
            {
                mat.SetTexture(MainTexPropId, texture);
                mat.SetTextureScale(MainTexPropId, safeTiling);
            }
        }
    }
}
