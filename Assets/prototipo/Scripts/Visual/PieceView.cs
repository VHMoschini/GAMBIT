using System.Collections;
using System.Collections.Generic;
using Prototipo.Core;
using TMPro;
using UnityEngine;

namespace Prototipo.Visual
{
    /// <summary>
    /// Representacao visual 3D de uma PieceInstance no tabuleiro:
    /// alterna entre capsula fechada e cilindro desbloqueado com visor frontal,
    /// exibe label 3D com billboard e executa animacoes suaves via Coroutines.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        private static readonly Color P1TeamColor = new Color(0.12f, 0.85f, 0.95f);
        private static readonly Color P2TeamColor = new Color(0.98f, 0.52f, 0.14f);
        private static readonly Color P1LabelColor = new Color(0.55f, 0.96f, 1.00f);
        private static readonly Color P2LabelColor = new Color(1.00f, 0.72f, 0.36f);
        private static readonly Color SelectionGoldColor = new Color(1.00f, 0.86f, 0.20f);
        private static readonly Color VisorColor = new Color(0.92f, 0.97f, 1.00f);
        private static readonly Color ExhaustedBodyColor = new Color(0.42f, 0.43f, 0.46f);
        private static readonly Color ExhaustedDetailColor = new Color(0.55f, 0.56f, 0.60f);
        private static readonly Color ExhaustedRingColor = new Color(0.32f, 0.34f, 0.38f);
        private static readonly Color ExhaustedLabelColor = new Color(0.68f, 0.70f, 0.74f);
        private static readonly Color HiddenCapsuleColor = new Color(0.28f, 0.30f, 0.36f);

        private sealed class RendererEntry
        {
            public Renderer Renderer;
            public Color NormalColor;
            public Color ExhaustedColor;
        }

        private readonly List<RendererEntry> _visualRenderers = new List<RendererEntry>();

        private Material _sharedMaterial;
        private MaterialPropertyBlock _propertyBlock;

        private GameObject _capsuleRoot;
        private GameObject _unlockedRoot;
        private GameObject _selectionRing;
        private TextMeshPro _label;

        private PieceSpec _spec;
        private bool _isUnlocked;
        private bool _hasActedThisTurn;
        private bool _hideClosedIdentity;
        private bool _isSelected;
        private bool _isDying;
        private bool _isPlayingMotion;
        private Vector3 _restingWorldPos;

        private Coroutine _motionCoroutine;
        private Coroutine _flashCoroutine;

        public int PieceId { get; private set; }
        public PlayerTeam Owner { get; private set; }

        private void Awake()
        {
            EnsureMaterial();
        }

        private void OnDestroy()
        {
            if (_sharedMaterial != null)
            {
                Destroy(_sharedMaterial);
                _sharedMaterial = null;
            }
        }

        private void Update()
        {
            if (_isDying || _isPlayingMotion)
            {
                return;
            }

            if (_isSelected)
            {
                float hoverOffset = Mathf.Sin(Time.time * 5.0f) * 0.05f + 0.06f;
                transform.position = _restingWorldPos + new Vector3(0f, hoverOffset, 0f);
            }
            else if ((transform.position - _restingWorldPos).sqrMagnitude > 0.0001f)
            {
                transform.position = _restingWorldPos;
            }
        }

        private void LateUpdate()
        {
            if (_label == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                _label.transform.rotation = cam.transform.rotation;
            }
        }

        /// <summary>
        /// Inicializa a peca visual a partir de uma PieceInstance logica.
        /// </summary>
        public void Initialize(PieceInstance piece, Vector3 worldPos, bool hideClosedIdentity = false)
        {
            EnsureMaterial();

            PieceId = piece.Id;
            Owner = piece.Owner;
            _spec = piece.Spec;
            _isUnlocked = piece.IsUnlocked;
            _hasActedThisTurn = piece.HasActedThisTurn;
            _hideClosedIdentity = hideClosedIdentity;
            _restingWorldPos = worldPos;
            transform.position = worldPos;

            BuildVisualHierarchy();
            ApplyUnlockedState(_isUnlocked);
            SetActedThisTurn(piece);
            SetSelected(false);
        }

        /// <summary>
        /// Sincroniza estado visual e texto com a instancia logica.
        /// </summary>
        public void SyncFromInstance(PieceInstance piece, Vector3 worldPos, bool immediate)
        {
            if (piece == null)
            {
                return;
            }

            _spec = piece.Spec;
            Owner = piece.Owner;
            _restingWorldPos = worldPos;

            if (immediate && !_isPlayingMotion && !_isDying)
            {
                transform.position = worldPos;
            }

            if (_isUnlocked != piece.IsUnlocked)
            {
                _isUnlocked = piece.IsUnlocked;
                ApplyUnlockedState(_isUnlocked);
            }

            SetActedThisTurn(piece);
        }

        /// <summary>
        /// Atualiza a aparencia cinza quando a peca ja interagiu neste turno (ou restaura a cor normal no proximo turno).
        /// </summary>
        public void SetActedThisTurn(PieceInstance piece)
        {
            if (piece == null || _isDying)
            {
                return;
            }

            _hasActedThisTurn = piece.HasActedThisTurn;
            if (_flashCoroutine == null)
            {
                RestoreRendererColors();
            }

            UpdateLabel(piece);
        }

        /// <summary>
        /// Ativa ou desativa o destaque visual de selecao.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_selectionRing != null)
            {
                _selectionRing.SetActive(selected);
            }

            if (!selected && !_isPlayingMotion && !_isDying)
            {
                transform.position = _restingWorldPos;
            }
        }

        /// <summary>
        /// Troca da forma de capsula fechada para cilindro ativo com efeito pop.
        /// </summary>
        public void PlayUnlockAnimation(PieceInstance piece)
        {
            _isUnlocked = true;
            ApplyUnlockedState(true);
            SetActedThisTurn(piece);

            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
            }

            _motionCoroutine = StartCoroutine(UnlockCoroutine());
        }

        /// <summary>
        /// Desliza suavemente a peca de from para to com leve arco vertical.
        /// </summary>
        public void PlayMoveAnimation(Vector3 from, Vector3 to)
        {
            _restingWorldPos = to;
            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
            }

            _motionCoroutine = StartCoroutine(MoveCoroutine(from, to));
        }

        /// <summary>
        /// Executa investida rapida em direcao ao alvo atacado.
        /// </summary>
        public void PlayAttackBump(Vector3 targetPos)
        {
            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
                transform.position = _restingWorldPos;
            }

            _motionCoroutine = StartCoroutine(AttackBumpCoroutine(targetPos));
        }

        /// <summary>
        /// Pisca em branco/vermelho ao receber dano e atualiza o texto de HP.
        /// </summary>
        public void PlayHitFlash(PieceInstance piece)
        {
            UpdateLabel(piece);

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(HitFlashCoroutine());
        }

        /// <summary>
        /// Encolhe a peca e a destroi apos morrer em combate.
        /// </summary>
        public void PlayDeathAndDestroy()
        {
            if (_isDying)
            {
                return;
            }

            _isDying = true;
            _isSelected = false;
            if (_selectionRing != null)
            {
                _selectionRing.SetActive(false);
            }

            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
            }

            StartCoroutine(DeathCoroutine());
        }

        private void BuildVisualHierarchy()
        {
            _visualRenderers.Clear();

            Color teamColor = Owner == PlayerTeam.Player1 ? P1TeamColor : P2TeamColor;
            Color exhaustedRing = Color.Lerp(teamColor, ExhaustedRingColor, 0.72f);
            Color pieceColor = _spec != null ? _spec.PieceColor : Color.white;
            Vector3 cylScale = _spec != null ? _spec.CylinderScale : new Vector3(0.60f, 0.65f, 0.60f);

            // Anel dourado de selecao
            _selectionRing = CreatePart(
                "SelectionRing",
                PrimitiveType.Cylinder,
                transform,
                new Vector3(0f, 0.015f, 0f),
                new Vector3(Mathf.Max(0.96f, cylScale.x + 0.28f), 0.018f, Mathf.Max(0.96f, cylScale.z + 0.28f)),
                SelectionGoldColor,
                SelectionGoldColor,
                false);
            _selectionRing.SetActive(false);

            // 1. Forma de Capsula Fechada
            _capsuleRoot = new GameObject("CapsuleForm");
            _capsuleRoot.transform.SetParent(transform, false);

            Color mutedCapsuleColor = _hideClosedIdentity
                ? HiddenCapsuleColor
                : Color.Lerp(pieceColor, new Color(0.22f, 0.22f, 0.26f), 0.45f);
            CreatePart(
                "CapsuleShell",
                PrimitiveType.Sphere,
                _capsuleRoot.transform,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(0.58f, 0.58f, 0.58f),
                mutedCapsuleColor,
                ExhaustedBodyColor,
                true);

            CreatePart(
                "CapsuleBand",
                PrimitiveType.Cylinder,
                _capsuleRoot.transform,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(0.61f, 0.05f, 0.61f),
                Color.white,
                ExhaustedDetailColor,
                true);

            CreatePart(
                "CapsuleTeamRing",
                PrimitiveType.Cylinder,
                _capsuleRoot.transform,
                new Vector3(0f, 0.03f, 0f),
                new Vector3(0.78f, 0.03f, 0.78f),
                teamColor,
                exhaustedRing,
                true);

            // 2. Forma Desbloqueada (Cilindro + Anel do Time + Visor Frontal)
            _unlockedRoot = new GameObject("UnlockedForm");
            _unlockedRoot.transform.SetParent(transform, false);
            _unlockedRoot.transform.localRotation = Quaternion.Euler(0f, Owner == PlayerTeam.Player1 ? 0f : 180f, 0f);

            CreatePart(
                "BodyCylinder",
                PrimitiveType.Cylinder,
                _unlockedRoot.transform,
                new Vector3(0f, cylScale.y, 0f),
                cylScale,
                pieceColor,
                ExhaustedBodyColor,
                true);

            float ringDiameter = Mathf.Max(0.82f, Mathf.Max(cylScale.x, cylScale.z) + 0.16f);
            CreatePart(
                "UnlockedTeamRing",
                PrimitiveType.Cylinder,
                _unlockedRoot.transform,
                new Vector3(0f, 0.035f, 0f),
                new Vector3(ringDiameter, 0.035f, ringDiameter),
                teamColor,
                exhaustedRing,
                true);

            CreatePart(
                "FrontVisor",
                PrimitiveType.Cube,
                _unlockedRoot.transform,
                new Vector3(0f, cylScale.y * 1.58f, cylScale.z * 0.42f),
                new Vector3(cylScale.x * 0.58f, 0.12f, 0.22f),
                VisorColor,
                ExhaustedDetailColor,
                true);

            // 3. Label flutuante 3D (TextMeshPro)
            GameObject labelGo = new GameObject("FloatingLabel");
            labelGo.transform.SetParent(transform, false);
            _label = labelGo.AddComponent<TextMeshPro>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 3.2f;
            _label.fontStyle = FontStyles.Bold;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.color = Owner == PlayerTeam.Player1 ? P1LabelColor : P2LabelColor;

            RectTransform rect = _label.rectTransform;
            rect.sizeDelta = new Vector2(4.0f, 1.0f);
        }

        private GameObject CreatePart(
            string partName,
            PrimitiveType primitive,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Color color,
            Color exhaustedColor,
            bool registerForFlash)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            // Remove collider para o raycast acertar sempre o tile embaixo da peca.
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _sharedMaterial;
                ApplyColor(rend, color);

                if (registerForFlash)
                {
                    _visualRenderers.Add(new RendererEntry
                    {
                        Renderer = rend,
                        NormalColor = color,
                        ExhaustedColor = exhaustedColor
                    });
                }
            }

            return go;
        }

        private void ApplyUnlockedState(bool unlocked)
        {
            if (_capsuleRoot != null)
            {
                _capsuleRoot.SetActive(!unlocked);
            }

            if (_unlockedRoot != null)
            {
                _unlockedRoot.SetActive(unlocked);
            }

            if (_label != null)
            {
                float height = unlocked && _spec != null ? _spec.CylinderScale.y * 2.0f + 0.38f : 0.98f;
                _label.transform.localPosition = new Vector3(0f, height, 0f);
            }
        }

        private void UpdateLabel(PieceInstance piece)
        {
            if (_label == null || piece == null)
            {
                return;
            }

            _label.color = _hasActedThisTurn
                ? ExhaustedLabelColor
                : (Owner == PlayerTeam.Player1 ? P1LabelColor : P2LabelColor);

            if (!piece.IsUnlocked)
            {
                _label.text = _hasActedThisTurn ? "CÁPSULA [AGIU]" : "CÁPSULA";
                return;
            }

            string initial = GetShortTypeCode(piece.Spec);
            string baseText = initial + "  HP: " + piece.CurrentHP + "/" + piece.Spec.MaxHP;
            _label.text = _hasActedThisTurn ? baseText + " [AGIU]" : baseText;
        }

        private static string GetShortTypeCode(PieceSpec spec)
        {
            if (spec == null)
            {
                return "?";
            }

            switch (spec.Id)
            {
                case "red":
                    return "V";
                case "blue":
                    return "A";
                case "yellow":
                    return "M";
                default:
                    if (!string.IsNullOrEmpty(spec.DisplayName))
                    {
                        return spec.DisplayName.Substring(0, 1).ToUpperInvariant();
                    }

                    return "?";
            }
        }

        private IEnumerator UnlockCoroutine()
        {
            _isPlayingMotion = true;
            if (_unlockedRoot != null)
            {
                const float duration = 0.32f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float scale = t < 0.65f
                        ? Mathf.Lerp(0.25f, 1.20f, t / 0.65f)
                        : Mathf.Lerp(1.20f, 1.00f, (t - 0.65f) / 0.35f);
                    _unlockedRoot.transform.localScale = Vector3.one * scale;
                    yield return null;
                }

                _unlockedRoot.transform.localScale = Vector3.one;
            }

            _isPlayingMotion = false;
            _motionCoroutine = null;
        }

        private IEnumerator MoveCoroutine(Vector3 from, Vector3 to)
        {
            _isPlayingMotion = true;
            const float duration = 0.26f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                Vector3 pos = Vector3.Lerp(from, to, eased);
                pos.y += Mathf.Sin(t * Mathf.PI) * 0.36f;
                transform.position = pos;
                yield return null;
            }

            transform.position = to;
            _isPlayingMotion = false;
            _motionCoroutine = null;
        }

        private IEnumerator AttackBumpCoroutine(Vector3 targetPos)
        {
            _isPlayingMotion = true;
            Vector3 startPos = _restingWorldPos;
            Vector3 lungePos = Vector3.Lerp(startPos, targetPos, 0.45f);
            lungePos.y += 0.10f;

            const float forwardTime = 0.10f;
            float elapsed = 0f;
            while (elapsed < forwardTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / forwardTime);
                transform.position = Vector3.Lerp(startPos, lungePos, t);
                yield return null;
            }

            const float returnTime = 0.14f;
            elapsed = 0f;
            while (elapsed < returnTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnTime);
                transform.position = Vector3.Lerp(lungePos, startPos, t);
                yield return null;
            }

            transform.position = startPos;
            _isPlayingMotion = false;
            _motionCoroutine = null;
        }

        private IEnumerator HitFlashCoroutine()
        {
            SetAllRenderersColor(Color.white);
            yield return new WaitForSeconds(0.08f);
            SetAllRenderersColor(new Color(1.0f, 0.25f, 0.25f));
            yield return new WaitForSeconds(0.12f);
            RestoreRendererColors();
            _flashCoroutine = null;
        }

        private IEnumerator DeathCoroutine()
        {
            const float duration = 0.28f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SetAllRenderersColor(Color color)
        {
            for (int i = 0; i < _visualRenderers.Count; i++)
            {
                ApplyColor(_visualRenderers[i].Renderer, color);
            }
        }

        private void RestoreRendererColors()
        {
            for (int i = 0; i < _visualRenderers.Count; i++)
            {
                Color targetColor = _hasActedThisTurn
                    ? _visualRenderers[i].ExhaustedColor
                    : _visualRenderers[i].NormalColor;
                ApplyColor(_visualRenderers[i].Renderer, targetColor);
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

            if (_sharedMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _sharedMaterial = new Material(shader);
            _sharedMaterial.name = "Piece_URPLit";
        }
    }
}
