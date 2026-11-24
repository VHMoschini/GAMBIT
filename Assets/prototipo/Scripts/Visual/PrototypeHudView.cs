using System;
using System.Collections.Generic;
using System.Text;
using Prototipo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Prototipo.Visual
{
    /// <summary>
    /// Conjunto de callbacks que conectam as acoes da interface uGUI ao controlador principal da partida.
    /// </summary>
    public sealed class PrototypeHudCallbacks
    {
        public Action<string> OnSelectSetupCapsule;
        public Action OnAutoFillSetup;
        public Action OnConfirmSetup;
        public Action OnUnlockSelectedCapsule;
        public Action OnEndTurn;
        public Action OnToggleCameraAutoFlip;
        public Action OnRestartMatch;

        // Multiplayer / Steam
        public Action OnPlayLocalMode;
        public Action OnCreateSteamLobby;
        public Action<string> OnJoinSteamLobby;
        public Action OnInviteSteamFriend;
        public Action OnLeaveSteamLobby;
        public Action OnRetrySteamInit;
    }

    /// <summary>
    /// HUD completo em uGUI + TextMeshPro construido programaticamente para o prototipo.
    /// Mantem o centro da tela livre para o tabuleiro 3D e garante EventSystem compativel com o New Input System.
    /// </summary>
    public sealed class PrototypeHudView : MonoBehaviour
    {
        private const int MaxLogEntries = 6;

        private static readonly Color P1Color = new Color(0.20f, 0.84f, 0.98f, 1f);
        private static readonly Color P2Color = new Color(0.98f, 0.56f, 0.16f, 1f);
        private static readonly Color PanelDark = new Color(0.06f, 0.08f, 0.13f, 0.90f);
        private static readonly Color CardDark = new Color(0.10f, 0.14f, 0.21f, 0.96f);
        private static readonly Color ButtonNeutral = new Color(0.18f, 0.23f, 0.33f, 1f);
        private static readonly Color ButtonPrimary = new Color(0.14f, 0.56f, 0.36f, 1f);
        private static readonly Color ButtonSteam = new Color(0.12f, 0.42f, 0.68f, 1f);
        private static readonly Color ButtonUnlock = new Color(0.78f, 0.54f, 0.12f, 1f);
        private static readonly Color ButtonDanger = new Color(0.56f, 0.18f, 0.20f, 1f);
        private static readonly Color TextPrimary = new Color(0.95f, 0.97f, 1.00f, 1f);
        private static readonly Color TextMuted = new Color(0.72f, 0.78f, 0.88f, 1f);

        private sealed class SetupPieceButtonEntry
        {
            public string SpecId;
            public Button Button;
            public Image Background;
            public Image SelectionBorder;
            public TextMeshProUGUI TitleText;
            public string BaseDisplayName;
        }

        private readonly List<SetupPieceButtonEntry> _setupButtons = new List<SetupPieceButtonEntry>();
        private readonly List<string> _logEntries = new List<string>();

        private PrototypeHudCallbacks _callbacks;
        private GameObject _canvasRoot;
        private bool _cameraAutoFlip = true;

        // Top Bar
        private Image _topBarAccent;
        private TextMeshProUGUI _phaseText;
        private TextMeshProUGUI _turnPlayerText;
        private TextMeshProUGUI _apText;
        private RectTransform _apFillRect;
        private Image _apFillImage;
        private TextMeshProUGUI _pieceCountText;
        private TextMeshProUGUI _steamTopButtonLabel;
        private TextMeshProUGUI _cameraAutoLabel;

        // Toast
        private GameObject _toastRoot;
        private TextMeshProUGUI _toastText;

        // Setup Panel
        private GameObject _setupPanelRoot;
        private Image _setupAccent;
        private TextMeshProUGUI _setupInstructionText;
        private Transform _setupButtonsContainer;
        private Button _autoFillButton;
        private Button _confirmSetupButton;
        private Image _confirmSetupImage;
        private TextMeshProUGUI _confirmSetupLabel;

        // Playing / Inspection Panel
        private GameObject _inspectPanelRoot;
        private Image _inspectAccent;
        private TextMeshProUGUI _inspectHintText;
        private GameObject _inspectDetailsRoot;
        private Image _inspectSwatch;
        private TextMeshProUGUI _inspectTitleText;
        private TextMeshProUGUI _inspectOwnerStateText;
        private TextMeshProUGUI _inspectStatsText;
        private TextMeshProUGUI _inspectSummaryText;
        private Button _unlockButton;
        private Image _unlockButtonImage;
        private TextMeshProUGUI _unlockButtonLabel;

        // End Turn
        private GameObject _endTurnRoot;
        private Image _endTurnAccent;
        private Button _endTurnButton;
        private TextMeshProUGUI _endTurnLabel;

        // Combat Log
        private TextMeshProUGUI _logBodyText;

        // Game Over Modal
        private GameObject _gameOverRoot;
        private Image _gameOverBorder;
        private TextMeshProUGUI _gameOverWinnerText;
        private TextMeshProUGUI _gameOverStatsText;

        // Multiplayer / Steam Modal
        private GameObject _multiplayerRoot;
        private TextMeshProUGUI _mpSteamStatusText;
        private Button _mpRetrySteamButton;
        private GameObject _mpNotInLobbyRoot;
        private Button _mpCreateLobbyButton;
        private TMP_InputField _mpLobbyCodeInput;
        private Button _mpJoinLobbyButton;
        private GameObject _mpInLobbyRoot;
        private TextMeshProUGUI _mpLobbyInfoText;
        private Button _mpCopyCodeButton;
        private TextMeshProUGUI _mpCopyCodeLabel;
        private Button _mpInviteFriendButton;
        private Button _mpBackToMatchButton;
        private Button _mpLeaveLobbyButton;
        private string _lastLobbyCode = string.Empty;

        public bool IsMultiplayerMenuOpen => _multiplayerRoot != null && _multiplayerRoot.activeSelf;

        public void SetMultiplayerMenuOpen(bool open)
        {
            if (_multiplayerRoot != null)
            {
                _multiplayerRoot.SetActive(open);
            }
        }

        /// <summary>
        /// Cria toda a hierarquia de UI do prototipo e conecta os botoes aos callbacks fornecidos.
        /// </summary>
        public void Initialize(MatchEngine engine, PrototypeHudCallbacks callbacks)
        {
            _callbacks = callbacks ?? new PrototypeHudCallbacks();
            _logEntries.Clear();
            _setupButtons.Clear();

            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                Destroy(_canvasRoot);
                _canvasRoot = null;
            }

            EnsureEventSystem();
            BuildCanvasHierarchy();
            BuildSetupCatalogButtons(engine != null && engine.Config != null ? engine.Config.Catalog : null);
            Refresh(engine, null, "red", string.Empty);
        }

        /// <summary>
        /// Atualiza todos os paineis do HUD de acordo com o estado atual do <see cref="MatchEngine"/> e da sessao Steam.
        /// </summary>
        public void Refresh(
            MatchEngine engine,
            PieceInstance selectedPiece,
            string selectedSetupSpecId,
            string statusToast,
            SteamMatchSession steamSession = null)
        {
            if (engine == null || _canvasRoot == null)
            {
                return;
            }

            PlayerTeam activeTeam = engine.ActiveTeam;
            Color teamColor = GetTeamColor(activeTeam);
            PlayerState activePlayer = engine.GetPlayer(activeTeam);

            bool isOnline = steamSession != null && steamSession.IsInLobby;
            bool canActNow = !isOnline || (steamSession.IsOnlineMatchActive && activeTeam == steamSession.LocalTeam);

            RefreshTopBar(engine, activeTeam, teamColor, activePlayer, steamSession);
            RefreshToast(statusToast);
            RefreshSetupPanel(engine, activeTeam, teamColor, selectedSetupSpecId, canActNow, steamSession);
            RefreshInspectionPanel(engine, activeTeam, teamColor, activePlayer, selectedPiece, canActNow);
            RefreshEndTurnButton(engine, teamColor, canActNow);
            RefreshGameOverModal(engine);
            RefreshMultiplayerModal(steamSession);
        }

        /// <summary>
        /// Adiciona uma entrada recente ao feed compacto de log de combate.
        /// </summary>
        public void AddLogEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            _logEntries.Add(entry.Trim());
            while (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }

            if (_logBodyText == null)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _logEntries.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                sb.Append("• ");
                sb.Append(_logEntries[i]);
            }

            _logBodyText.text = sb.ToString();
        }

        /// <summary>
        /// Permite sincronizar externamente o rotulo do botao de auto-rotacao da camera, se necessario.
        /// </summary>
        public void SetCameraAutoFlipState(bool enabled)
        {
            _cameraAutoFlip = enabled;
            UpdateCameraAutoLabel();
        }

        private void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current;
            if (existing == null)
            {
                existing = Object.FindAnyObjectByType<EventSystem>();
            }

            if (existing == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                existing = esGo.AddComponent<EventSystem>();
            }

            StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Destroy(legacy);
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void BuildCanvasHierarchy()
        {
            _canvasRoot = new GameObject("PrototipoHudCanvas");
            _canvasRoot.transform.SetParent(transform, false);

            Canvas canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRoot.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = _canvasRoot.GetComponent<RectTransform>();
            BuildTopBar(rootRect);
            BuildToast(rootRect);
            BuildSetupPanel(rootRect);
            BuildInspectionPanel(rootRect);
            BuildEndTurnButton(rootRect);
            BuildCombatLogPanel(rootRect);
            BuildGameOverModal(rootRect);
            BuildMultiplayerModal(rootRect);
        }

        private void BuildTopBar(RectTransform parent)
        {
            GameObject barGo = CreatePanel("TopBar", parent, PanelDark, raycastTarget: true);
            RectTransform barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 84f);

            GameObject accentGo = CreatePanel("TopBarAccent", barRect, P1Color, raycastTarget: false);
            RectTransform accentRect = accentGo.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(1f, 0f);
            accentRect.pivot = new Vector2(0.5f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 4f);
            _topBarAccent = accentGo.GetComponent<Image>();

            _phaseText = CreateText("PhaseText", barRect, 15f, FontStyles.Bold, TextMuted, TextAlignmentOptions.MidlineLeft);
            SetAnchoredBox(_phaseText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 18f), new Vector2(460f, 24f));

            _turnPlayerText = CreateText("TurnPlayerText", barRect, 24f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
            SetAnchoredBox(_turnPlayerText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, -12f), new Vector2(460f, 34f));

            _apText = CreateText("ApText", barRect, 21f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
            SetAnchoredBox(_apText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-210f, 14f), new Vector2(340f, 28f));

            GameObject apTrack = CreatePanel("ApTrack", barRect, new Color(0.16f, 0.20f, 0.28f, 1f), raycastTarget: false);
            RectTransform trackRect = apTrack.GetComponent<RectTransform>();
            SetAnchoredBox(trackRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-210f, -16f), new Vector2(340f, 16f));

            GameObject apFill = CreatePanel("ApFill", trackRect, P1Color, raycastTarget: false);
            _apFillRect = apFill.GetComponent<RectTransform>();
            _apFillRect.anchorMin = new Vector2(0f, 0f);
            _apFillRect.anchorMax = new Vector2(1f, 1f);
            _apFillRect.offsetMin = new Vector2(2f, 2f);
            _apFillRect.offsetMax = new Vector2(-2f, -2f);
            _apFillImage = apFill.GetComponent<Image>();

            _pieceCountText = CreateText("PieceCountText", barRect, 19f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Center);
            SetAnchoredBox(_pieceCountText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(105f, 0f), new Vector2(250f, 44f));

            Button steamButton = CreateButton("SteamMenuButton", barRect, "Steam / Online", ButtonSteam, 16f, OnToggleMultiplayerMenuClicked, out _steamTopButtonLabel);
            SetAnchoredBox(steamButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-416f, 0f), new Vector2(205f, 48f));

            Button camButton = CreateButton("CameraAutoButton", barRect, "Câmera Auto: ON", ButtonNeutral, 16f, OnCameraAutoClicked, out _cameraAutoLabel);
            SetAnchoredBox(camButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-194f, 0f), new Vector2(210f, 48f));
            UpdateCameraAutoLabel();

            Button restartButton = CreateButton("RestartButton", barRect, "Reiniciar", ButtonDanger, 17f, OnRestartClicked, out _);
            SetAnchoredBox(restartButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(156f, 48f));
        }

        private void BuildToast(RectTransform parent)
        {
            _toastRoot = CreatePanel("StatusToast", parent, new Color(0.07f, 0.10f, 0.16f, 0.88f), raycastTarget: false);
            RectTransform toastRect = _toastRoot.GetComponent<RectTransform>();
            SetAnchoredBox(toastRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(780f, 44f));

            _toastText = CreateText("ToastText", toastRect, 19f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Center);
            StretchFill(_toastText.rectTransform, 16f, 4f);
            _toastRoot.SetActive(false);
        }

        private void BuildSetupPanel(RectTransform parent)
        {
            _setupPanelRoot = CreatePanel("SetupPanel", parent, PanelDark, raycastTarget: true);
            RectTransform panelRect = _setupPanelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -104f);
            panelRect.sizeDelta = new Vector2(430f, 0f);

            VerticalLayoutGroup layout = _setupPanelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _setupPanelRoot.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject accentGo = CreatePanel("SetupAccent", panelRect, P1Color, raycastTarget: false);
            AddLayoutElement(accentGo, minHeight: 4f, preferredHeight: 4f);
            _setupAccent = accentGo.GetComponent<Image>();

            TextMeshProUGUI title = CreateText("SetupTitle", panelRect, 20f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
            title.text = "ESCOLHA DE CÁPSULAS";
            AddLayoutElement(title.gameObject, minHeight: 28f, preferredHeight: 28f);

            _setupInstructionText = CreateText("SetupInstruction", panelRect, 16f, FontStyles.Normal, TextMuted, TextAlignmentOptions.TopLeft);
            _setupInstructionText.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(_setupInstructionText.gameObject, minHeight: 54f, preferredHeight: 54f);

            GameObject buttonsContainer = new GameObject("CapsuleButtonsContainer");
            buttonsContainer.transform.SetParent(panelRect, false);
            buttonsContainer.AddComponent<RectTransform>();
            VerticalLayoutGroup btnLayout = buttonsContainer.AddComponent<VerticalLayoutGroup>();
            btnLayout.spacing = 8f;
            btnLayout.childControlWidth = true;
            btnLayout.childControlHeight = true;
            btnLayout.childForceExpandWidth = true;
            btnLayout.childForceExpandHeight = false;
            ContentSizeFitter btnFitter = buttonsContainer.AddComponent<ContentSizeFitter>();
            btnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _setupButtonsContainer = buttonsContainer.transform;

            _autoFillButton = CreateButton("AutoFillButton", panelRect, "Auto-Preencher", ButtonNeutral, 18f, OnAutoFillClicked, out _);
            AddLayoutElement(_autoFillButton.gameObject, minHeight: 46f, preferredHeight: 46f);

            _confirmSetupButton = CreateButton("ConfirmSetupButton", panelRect, "Confirmar Posicionamento", ButtonPrimary, 19f, OnConfirmSetupClicked, out _confirmSetupLabel);
            _confirmSetupImage = _confirmSetupButton.GetComponent<Image>();
            AddLayoutElement(_confirmSetupButton.gameObject, minHeight: 52f, preferredHeight: 52f);
        }

        private void BuildSetupCatalogButtons(IReadOnlyList<PieceSpec> catalog)
        {
            if (_setupButtonsContainer == null)
            {
                return;
            }

            for (int i = _setupButtonsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_setupButtonsContainer.GetChild(i).gameObject);
            }
            _setupButtons.Clear();

            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                PieceSpec spec = catalog[i];
                if (spec == null)
                {
                    continue;
                }

                string specId = spec.Id;
                GameObject btnGo = CreatePanel("CapsuleOption_" + specId, _setupButtonsContainer, CardDark, raycastTarget: true);
                AddLayoutElement(btnGo, minHeight: 92f, preferredHeight: 92f);

                Button btn = btnGo.AddComponent<Button>();
                Image bg = btnGo.GetComponent<Image>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(delegate
                {
                    if (_callbacks != null && _callbacks.OnSelectSetupCapsule != null)
                    {
                        _callbacks.OnSelectSetupCapsule.Invoke(specId);
                    }
                });

                RectTransform btnRect = btnGo.GetComponent<RectTransform>();

                // Borda de destaque quando selecionado
                GameObject borderGo = CreatePanel("SelectionHighlight", btnRect, new Color(1f, 1f, 1f, 0.25f), raycastTarget: false);
                RectTransform borderRect = borderGo.GetComponent<RectTransform>();
                StretchFill(borderRect, 0f, 0f);
                Image selectionBorder = borderGo.GetComponent<Image>();
                selectionBorder.enabled = false;

                // Faixa lateral com a cor real da peca
                GameObject swatchGo = CreatePanel("Swatch", btnRect, spec.PieceColor, raycastTarget: false);
                RectTransform swatchRect = swatchGo.GetComponent<RectTransform>();
                swatchRect.anchorMin = new Vector2(0f, 0f);
                swatchRect.anchorMax = new Vector2(0f, 1f);
                swatchRect.pivot = new Vector2(0f, 0.5f);
                swatchRect.anchoredPosition = Vector2.zero;
                swatchRect.sizeDelta = new Vector2(12f, 0f);

                TextMeshProUGUI titleTmp = CreateText("Title", btnRect, 18f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
                SetAnchoredBox(titleTmp.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22f, -8f), new Vector2(-30f, 26f));
                titleTmp.text = spec.DisplayName;

                TextMeshProUGUI statsTmp = CreateText("Stats", btnRect, 14f, FontStyles.Bold, new Color(0.84f, 0.90f, 0.98f, 1f), TextAlignmentOptions.MidlineLeft);
                SetAnchoredBox(statsTmp.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22f, -36f), new Vector2(-30f, 22f));
                statsTmp.text = string.Format("HP {0}  |  ATK {1}  |  DEF {2}  |  Alcance {3}", spec.MaxHP, spec.Attack, spec.Defense, spec.AttackRange);

                TextMeshProUGUI costsTmp = CreateText("Costs", btnRect, 13f, FontStyles.Normal, TextMuted, TextAlignmentOptions.MidlineLeft);
                SetAnchoredBox(costsTmp.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22f, -60f), new Vector2(-30f, 22f));
                costsTmp.text = string.Format("Desbloq: {0} AP  |  Mover: {1} AP  |  Atacar: {2} AP", spec.UnlockCost, spec.MoveCost, spec.AttackCost);

                SetupPieceButtonEntry entry = new SetupPieceButtonEntry
                {
                    SpecId = specId,
                    Button = btn,
                    Background = bg,
                    SelectionBorder = selectionBorder,
                    TitleText = titleTmp,
                    BaseDisplayName = spec.DisplayName
                };
                _setupButtons.Add(entry);
            }
        }

        private void BuildInspectionPanel(RectTransform parent)
        {
            _inspectPanelRoot = CreatePanel("InspectionPanel", parent, PanelDark, raycastTarget: true);
            RectTransform panelRect = _inspectPanelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -104f);
            panelRect.sizeDelta = new Vector2(410f, 0f);

            VerticalLayoutGroup layout = _inspectPanelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _inspectPanelRoot.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject accentGo = CreatePanel("InspectAccent", panelRect, P1Color, raycastTarget: false);
            AddLayoutElement(accentGo, minHeight: 4f, preferredHeight: 4f);
            _inspectAccent = accentGo.GetComponent<Image>();

            TextMeshProUGUI header = CreateText("InspectHeader", panelRect, 20f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
            header.text = "UNIDADE SELECIONADA";
            AddLayoutElement(header.gameObject, minHeight: 28f, preferredHeight: 28f);

            _inspectHintText = CreateText("InspectHint", panelRect, 16f, FontStyles.Normal, TextMuted, TextAlignmentOptions.TopLeft);
            _inspectHintText.textWrappingMode = TextWrappingModes.Normal;
            _inspectHintText.text = "Clique em uma cápsula ou peça sua.\nCada peça age 1x por turno (desencapsular, mover ou atacar).\nCiano = Mover  |  Vermelho = Atacar";
            AddLayoutElement(_inspectHintText.gameObject, minHeight: 84f, preferredHeight: 84f);

            _inspectDetailsRoot = new GameObject("InspectDetails");
            _inspectDetailsRoot.transform.SetParent(panelRect, false);
            RectTransform detailsRect = _inspectDetailsRoot.AddComponent<RectTransform>();
            VerticalLayoutGroup detailsLayout = _inspectDetailsRoot.AddComponent<VerticalLayoutGroup>();
            detailsLayout.spacing = 8f;
            detailsLayout.childControlWidth = true;
            detailsLayout.childControlHeight = true;
            detailsLayout.childForceExpandWidth = true;
            detailsLayout.childForceExpandHeight = false;
            ContentSizeFitter detailsFitter = _inspectDetailsRoot.AddComponent<ContentSizeFitter>();
            detailsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(detailsRect, false);
            titleRow.AddComponent<RectTransform>();
            AddLayoutElement(titleRow, minHeight: 30f, preferredHeight: 30f);
            HorizontalLayoutGroup rowLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            GameObject swatchGo = CreatePanel("InspectSwatch", titleRow.transform, Color.white, raycastTarget: false);
            AddLayoutElement(swatchGo, minHeight: 26f, preferredHeight: 26f, minWidth: 16f, preferredWidth: 16f);
            _inspectSwatch = swatchGo.GetComponent<Image>();

            _inspectTitleText = CreateText("InspectTitle", titleRow.transform, 21f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.MidlineLeft);
            AddLayoutElement(_inspectTitleText.gameObject, minHeight: 28f, preferredHeight: 28f, flexibleWidth: 1f);

            _inspectOwnerStateText = CreateText("InspectOwnerState", detailsRect, 16f, FontStyles.Bold, TextMuted, TextAlignmentOptions.MidlineLeft);
            AddLayoutElement(_inspectOwnerStateText.gameObject, minHeight: 24f, preferredHeight: 24f);

            _inspectStatsText = CreateText("InspectStats", detailsRect, 15f, FontStyles.Normal, TextPrimary, TextAlignmentOptions.TopLeft);
            _inspectStatsText.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(_inspectStatsText.gameObject, minHeight: 68f, preferredHeight: 68f);

            _inspectSummaryText = CreateText("InspectSummary", detailsRect, 14f, FontStyles.Italic, TextMuted, TextAlignmentOptions.TopLeft);
            _inspectSummaryText.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(_inspectSummaryText.gameObject, minHeight: 42f, preferredHeight: 42f);

            _unlockButton = CreateButton("UnlockButton", detailsRect, "Desbloquear Cápsula", ButtonUnlock, 18f, OnUnlockClicked, out _unlockButtonLabel);
            _unlockButtonImage = _unlockButton.GetComponent<Image>();
            AddLayoutElement(_unlockButton.gameObject, minHeight: 50f, preferredHeight: 50f);
        }

        private void BuildEndTurnButton(RectTransform parent)
        {
            _endTurnRoot = CreatePanel("EndTurnContainer", parent, PanelDark, raycastTarget: true);
            RectTransform containerRect = _endTurnRoot.GetComponent<RectTransform>();
            SetAnchoredBox(containerRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(280f, 76f));

            GameObject accent = CreatePanel("EndTurnAccent", containerRect, P1Color, raycastTarget: false);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 4f);
            _endTurnAccent = accent.GetComponent<Image>();

            _endTurnButton = CreateButton("EndTurnButton", containerRect, "Encerrar Turno", ButtonPrimary, 23f, OnEndTurnClicked, out _endTurnLabel);
            RectTransform btnRect = _endTurnButton.GetComponent<RectTransform>();
            btnRect.anchorMin = Vector2.zero;
            btnRect.anchorMax = Vector2.one;
            btnRect.offsetMin = new Vector2(6f, 6f);
            btnRect.offsetMax = new Vector2(-6f, -8f);
        }

        private void BuildCombatLogPanel(RectTransform parent)
        {
            GameObject logGo = CreatePanel("CombatLogPanel", parent, new Color(0.05f, 0.07f, 0.11f, 0.84f), raycastTarget: true);
            RectTransform logRect = logGo.GetComponent<RectTransform>();
            SetAnchoredBox(logRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(510f, 178f));

            TextMeshProUGUI header = CreateText("LogHeader", logRect, 13f, FontStyles.Bold, TextMuted, TextAlignmentOptions.MidlineLeft);
            header.text = "REGISTRO DE COMBATE";
            SetAnchoredBox(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(-28f, 22f));

            _logBodyText = CreateText("LogBody", logRect, 14f, FontStyles.Normal, TextPrimary, TextAlignmentOptions.TopLeft);
            _logBodyText.textWrappingMode = TextWrappingModes.Normal;
            _logBodyText.text = "• Aguardando posicionamento inicial...";
            SetAnchoredBox(_logBodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(-28f, -38f));
        }

        private void BuildGameOverModal(RectTransform parent)
        {
            _gameOverRoot = CreatePanel("GameOverOverlay", parent, new Color(0.02f, 0.03f, 0.06f, 0.78f), raycastTarget: true);
            RectTransform overlayRect = _gameOverRoot.GetComponent<RectTransform>();
            StretchFill(overlayRect, 0f, 0f);

            GameObject borderGo = CreatePanel("GameOverBorder", overlayRect, P1Color, raycastTarget: true);
            RectTransform borderRect = borderGo.GetComponent<RectTransform>();
            SetAnchoredBox(borderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 340f));
            _gameOverBorder = borderGo.GetComponent<Image>();

            GameObject cardGo = CreatePanel("GameOverCard", borderRect, new Color(0.07f, 0.10f, 0.16f, 0.98f), raycastTarget: true);
            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            StretchFill(cardRect, 4f, 4f);

            _gameOverWinnerText = CreateText("WinnerTitle", cardRect, 36f, FontStyles.Bold, P1Color, TextAlignmentOptions.Center);
            SetAnchoredBox(_gameOverWinnerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-40f, 64f));

            _gameOverStatsText = CreateText("WinnerStats", cardRect, 20f, FontStyles.Normal, TextPrimary, TextAlignmentOptions.Center);
            _gameOverStatsText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchoredBox(_gameOverStatsText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(-60f, 84f));

            Button playAgainButton = CreateButton("PlayAgainButton", cardRect, "Jogar Novamente", ButtonPrimary, 22f, OnRestartClicked, out _);
            SetAnchoredBox(playAgainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(280f, 64f));

            _gameOverRoot.SetActive(false);
        }

        private void BuildMultiplayerModal(RectTransform parent)
        {
            _multiplayerRoot = CreatePanel("MultiplayerOverlay", parent, new Color(0.02f, 0.03f, 0.06f, 0.80f), raycastTarget: true);
            RectTransform overlayRect = _multiplayerRoot.GetComponent<RectTransform>();
            StretchFill(overlayRect, 0f, 0f);

            GameObject borderGo = CreatePanel("MultiplayerBorder", overlayRect, ButtonSteam, raycastTarget: true);
            RectTransform borderRect = borderGo.GetComponent<RectTransform>();
            SetAnchoredBox(borderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 590f));

            GameObject cardGo = CreatePanel("MultiplayerCard", borderRect, new Color(0.07f, 0.10f, 0.16f, 0.98f), raycastTarget: true);
            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            StretchFill(cardRect, 4f, 4f);

            VerticalLayoutGroup layout = cardGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateText("MpTitle", cardRect, 26f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Center);
            title.text = "GAMBIT PROTÓTIPO — LOCAL & STEAM";
            AddLayoutElement(title.gameObject, minHeight: 36f, preferredHeight: 36f);

            _mpSteamStatusText = CreateText("MpSteamStatus", cardRect, 15f, FontStyles.Bold, TextMuted, TextAlignmentOptions.Center);
            _mpSteamStatusText.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(_mpSteamStatusText.gameObject, minHeight: 42f, preferredHeight: 42f);

            _mpRetrySteamButton = CreateButton("MpRetrySteamButton", cardRect, "Tentar Reconectar Steam", ButtonNeutral, 16f, OnRetrySteamClicked, out _);
            AddLayoutElement(_mpRetrySteamButton.gameObject, minHeight: 38f, preferredHeight: 38f);

            Button localButton = CreateButton("MpLocalButton", cardRect, "Jogar Local (2 Jogadores neste PC)", ButtonPrimary, 19f, OnPlayLocalClicked, out _);
            AddLayoutElement(localButton.gameObject, minHeight: 52f, preferredHeight: 52f);

            GameObject divider = CreatePanel("MpDivider", cardRect, new Color(0.24f, 0.30f, 0.42f, 0.9f), raycastTarget: false);
            AddLayoutElement(divider, minHeight: 2f, preferredHeight: 2f);

            TextMeshProUGUI steamHeader = CreateText("MpSteamHeader", cardRect, 18f, FontStyles.Bold, P1Color, TextAlignmentOptions.Center);
            steamHeader.text = "STEAM MULTIPLAYER (APPID 480 / SPACEWAR)";
            AddLayoutElement(steamHeader.gameObject, minHeight: 28f, preferredHeight: 28f);

            // Estado 1: Fora de uma sala Steam
            _mpNotInLobbyRoot = new GameObject("MpNotInLobby");
            _mpNotInLobbyRoot.transform.SetParent(cardRect, false);
            _mpNotInLobbyRoot.AddComponent<RectTransform>();
            VerticalLayoutGroup notInLayout = _mpNotInLobbyRoot.AddComponent<VerticalLayoutGroup>();
            notInLayout.spacing = 10f;
            notInLayout.childControlWidth = true;
            notInLayout.childControlHeight = true;
            notInLayout.childForceExpandWidth = true;
            notInLayout.childForceExpandHeight = false;
            ContentSizeFitter notInFitter = _mpNotInLobbyRoot.AddComponent<ContentSizeFitter>();
            notInFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _mpCreateLobbyButton = CreateButton("MpCreateLobbyButton", _mpNotInLobbyRoot.transform, "Criar Sala Steam (Você = Jogador 1)", ButtonSteam, 18f, OnCreateSteamLobbyClicked, out _);
            AddLayoutElement(_mpCreateLobbyButton.gameObject, minHeight: 50f, preferredHeight: 50f);

            TextMeshProUGUI joinLabel = CreateText("MpJoinLabel", _mpNotInLobbyRoot.transform, 15f, FontStyles.Normal, TextMuted, TextAlignmentOptions.MidlineLeft);
            joinLabel.text = "Ou entre com o código da sala do seu amigo (ou aceite o convite no chat da Steam):";
            joinLabel.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(joinLabel.gameObject, minHeight: 36f, preferredHeight: 36f);

            GameObject joinRow = new GameObject("MpJoinRow");
            joinRow.transform.SetParent(_mpNotInLobbyRoot.transform, false);
            joinRow.AddComponent<RectTransform>();
            AddLayoutElement(joinRow, minHeight: 48f, preferredHeight: 48f);
            HorizontalLayoutGroup joinRowLayout = joinRow.AddComponent<HorizontalLayoutGroup>();
            joinRowLayout.spacing = 10f;
            joinRowLayout.childControlWidth = true;
            joinRowLayout.childControlHeight = true;
            joinRowLayout.childForceExpandWidth = false;
            joinRowLayout.childForceExpandHeight = true;

            _mpLobbyCodeInput = CreateInputField("MpLobbyCodeInput", joinRow.transform, "Cole o código numérico da sala...");
            AddLayoutElement(_mpLobbyCodeInput.gameObject, minHeight: 48f, preferredHeight: 48f, flexibleWidth: 1f);

            _mpJoinLobbyButton = CreateButton("MpJoinLobbyButton", joinRow.transform, "Entrar (P2)", ButtonUnlock, 17f, OnJoinSteamLobbyClicked, out _);
            AddLayoutElement(_mpJoinLobbyButton.gameObject, minHeight: 48f, preferredHeight: 48f, minWidth: 150f, preferredWidth: 150f);

            // Estado 2: Dentro de uma sala Steam
            _mpInLobbyRoot = new GameObject("MpInLobby");
            _mpInLobbyRoot.transform.SetParent(cardRect, false);
            _mpInLobbyRoot.AddComponent<RectTransform>();
            VerticalLayoutGroup inLayout = _mpInLobbyRoot.AddComponent<VerticalLayoutGroup>();
            inLayout.spacing = 10f;
            inLayout.childControlWidth = true;
            inLayout.childControlHeight = true;
            inLayout.childForceExpandWidth = true;
            inLayout.childForceExpandHeight = false;
            ContentSizeFitter inFitter = _mpInLobbyRoot.AddComponent<ContentSizeFitter>();
            inFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _mpLobbyInfoText = CreateText("MpLobbyInfo", _mpInLobbyRoot.transform, 16f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Center);
            _mpLobbyInfoText.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(_mpLobbyInfoText.gameObject, minHeight: 62f, preferredHeight: 62f);

            GameObject lobbyActionsRow = new GameObject("MpLobbyActionsRow");
            lobbyActionsRow.transform.SetParent(_mpInLobbyRoot.transform, false);
            lobbyActionsRow.AddComponent<RectTransform>();
            AddLayoutElement(lobbyActionsRow, minHeight: 48f, preferredHeight: 48f);
            HorizontalLayoutGroup lobbyRowLayout = lobbyActionsRow.AddComponent<HorizontalLayoutGroup>();
            lobbyRowLayout.spacing = 10f;
            lobbyRowLayout.childControlWidth = true;
            lobbyRowLayout.childControlHeight = true;
            lobbyRowLayout.childForceExpandWidth = true;
            lobbyRowLayout.childForceExpandHeight = true;

            _mpCopyCodeButton = CreateButton("MpCopyCodeButton", lobbyActionsRow.transform, "Copiar Código", ButtonNeutral, 17f, OnCopyLobbyCodeClicked, out _mpCopyCodeLabel);
            _mpInviteFriendButton = CreateButton("MpInviteFriendButton", lobbyActionsRow.transform, "Convidar Amigo (Steam)", ButtonSteam, 17f, OnInviteSteamFriendClicked, out _);

            _mpBackToMatchButton = CreateButton("MpBackToMatchButton", _mpInLobbyRoot.transform, "Voltar ao Tabuleiro", ButtonPrimary, 18f, OnCloseMultiplayerMenuClicked, out _);
            AddLayoutElement(_mpBackToMatchButton.gameObject, minHeight: 46f, preferredHeight: 46f);

            _mpLeaveLobbyButton = CreateButton("MpLeaveLobbyButton", _mpInLobbyRoot.transform, "Sair da Sala Steam", ButtonDanger, 17f, OnLeaveSteamLobbyClicked, out _);
            AddLayoutElement(_mpLeaveLobbyButton.gameObject, minHeight: 44f, preferredHeight: 44f);

            _mpInLobbyRoot.SetActive(false);
            _multiplayerRoot.SetActive(true);
        }

        private void RefreshTopBar(
            MatchEngine engine,
            PlayerTeam activeTeam,
            Color teamColor,
            PlayerState activePlayer,
            SteamMatchSession steamSession)
        {
            _topBarAccent.color = teamColor;

            bool isOnline = steamSession != null && steamSession.IsInLobby;
            string modePrefix = isOnline
                ? (steamSession.IsOnlineMatchActive
                    ? "ONLINE (" + steamSession.LocalTeam.ToDisplayName() + " vs " + steamSession.OpponentName + ")  •  "
                    : "SALA STEAM (AGUARDANDO)  •  ")
                : "LOCAL  •  ";
            _phaseText.text = modePrefix + FormatPhase(engine.Phase);

            if (_steamTopButtonLabel != null)
            {
                _steamTopButtonLabel.text = isOnline ? "Steam: Sala Ativa" : "Steam / Online";
            }

            string teamHex = ColorUtility.ToHtmlStringRGB(teamColor);
            if (engine.Phase == GamePhase.Setup)
            {
                _turnPlayerText.text = string.Format("Setup  •  <color=#{0}>{1}</color>", teamHex, activeTeam.ToDisplayName());
            }
            else if (engine.Phase == GamePhase.GameOver && engine.Winner.HasValue)
            {
                Color winColor = GetTeamColor(engine.Winner.Value);
                string winHex = ColorUtility.ToHtmlStringRGB(winColor);
                _turnPlayerText.text = string.Format("Turno {0}  •  <color=#{1}>Vitória: {2}</color>", engine.TurnNumber, winHex, engine.Winner.Value.ToDisplayName());
            }
            else
            {
                _turnPlayerText.text = string.Format("Turno {0}  •  <color=#{1}>{2}</color>", engine.TurnNumber, teamHex, activeTeam.ToDisplayName());
            }

            if (activePlayer != null && activePlayer.MaxAP > 0)
            {
                _apText.text = string.Format("Pontos de Ação (AP): {0} / {1}", activePlayer.CurrentAP, activePlayer.MaxAP);
                float ratio = Mathf.Clamp01((float)activePlayer.CurrentAP / activePlayer.MaxAP);
                _apFillRect.anchorMax = new Vector2(ratio, 1f);
                _apFillImage.color = teamColor;
            }
            else
            {
                _apText.text = "Pontos de Ação (AP): --";
                _apFillRect.anchorMax = new Vector2(0f, 1f);
            }

            int p1Alive = engine.GetPieceCount(PlayerTeam.Player1, aliveOnly: true);
            int p2Alive = engine.GetPieceCount(PlayerTeam.Player2, aliveOnly: true);
            string p1Hex = ColorUtility.ToHtmlStringRGB(P1Color);
            string p2Hex = ColorUtility.ToHtmlStringRGB(P2Color);
            _pieceCountText.text = string.Format("<color=#{0}>P1: {1}</color>   |   <color=#{2}>P2: {3}</color>", p1Hex, p1Alive, p2Hex, p2Alive);
        }

        private void RefreshToast(string statusToast)
        {
            bool hasToast = !string.IsNullOrWhiteSpace(statusToast);
            _toastRoot.SetActive(hasToast);
            if (hasToast)
            {
                _toastText.text = statusToast;
            }
        }

        private void RefreshSetupPanel(
            MatchEngine engine,
            PlayerTeam activeTeam,
            Color teamColor,
            string selectedSetupSpecId,
            bool canActNow,
            SteamMatchSession steamSession)
        {
            bool isSetup = engine.Phase == GamePhase.Setup;
            _setupPanelRoot.SetActive(isSetup);
            if (!isSetup)
            {
                return;
            }

            _setupAccent.color = teamColor;
            int remaining = engine.GetRemainingSetupCapsules(activeTeam);
            if (!canActNow)
            {
                string oppName = steamSession != null && !string.IsNullOrEmpty(steamSession.OpponentName)
                    ? steamSession.OpponentName
                    : activeTeam.ToDisplayName();
                _setupInstructionText.text = "Aguardando " + oppName + " posicionar as cápsulas...";
            }
            else
            {
                _setupInstructionText.text = string.Format(
                    "{0}: escolha a cápsula e clique na sua zona destacada (restam {1}). Clique direito remove.",
                    activeTeam.ToDisplayName(),
                    remaining);
            }

            for (int i = 0; i < _setupButtons.Count; i++)
            {
                SetupPieceButtonEntry entry = _setupButtons[i];
                bool isSelected = string.Equals(entry.SpecId, selectedSetupSpecId, StringComparison.OrdinalIgnoreCase);
                entry.Button.interactable = canActNow;
                entry.SelectionBorder.enabled = isSelected && canActNow;
                entry.SelectionBorder.color = new Color(teamColor.r, teamColor.g, teamColor.b, 0.22f);
                entry.Background.color = isSelected && canActNow ? new Color(0.16f, 0.23f, 0.35f, 0.98f) : CardDark;
                entry.TitleText.text = isSelected && canActNow ? entry.BaseDisplayName + "  [SELECIONADA]" : entry.BaseDisplayName;
            }

            if (_autoFillButton != null)
            {
                _autoFillButton.interactable = canActNow && remaining > 0;
            }

            bool canConfirm = canActNow && engine.CanConfirmSetup(activeTeam);
            _confirmSetupButton.interactable = canConfirm;
            _confirmSetupImage.color = canConfirm ? ButtonPrimary : new Color(0.22f, 0.25f, 0.30f, 0.75f);
            _confirmSetupLabel.color = canConfirm ? TextPrimary : TextMuted;
        }

        private void RefreshInspectionPanel(
            MatchEngine engine,
            PlayerTeam activeTeam,
            Color teamColor,
            PlayerState activePlayer,
            PieceInstance selectedPiece,
            bool canActNow)
        {
            bool isPlaying = engine.Phase == GamePhase.Playing;
            _inspectPanelRoot.SetActive(isPlaying);
            if (!isPlaying)
            {
                return;
            }

            _inspectAccent.color = teamColor;
            bool hasPiece = selectedPiece != null && selectedPiece.IsAlive;
            _inspectHintText.gameObject.SetActive(!hasPiece);
            _inspectDetailsRoot.SetActive(hasPiece);

            if (!hasPiece)
            {
                _inspectHintText.text = canActNow
                    ? "Clique em uma cápsula ou peça sua.\nCada peça age 1x por turno (desencapsular, mover ou atacar).\nCiano = Mover  |  Vermelho = Atacar"
                    : "Aguarde o turno do oponente...\nVocê só controla as peças do seu time.";
                return;
            }

            PieceSpec spec = selectedPiece.Spec;
            _inspectSwatch.color = spec.PieceColor;
            _inspectTitleText.text = spec.DisplayName;

            string ownerHex = ColorUtility.ToHtmlStringRGB(GetTeamColor(selectedPiece.Owner));
            string stateLabel = selectedPiece.HasActedThisTurn
                ? "<color=#9CA3AF>JÁ AGIU NESTE TURNO</color>"
                : (selectedPiece.IsUnlocked
                    ? "<color=#5EEAD4>ATIVA</color>"
                    : "<color=#FBBF24>CÁPSULA FECHADA</color>");
            _inspectOwnerStateText.text = string.Format("<color=#{0}>{1}</color>  •  {2}", ownerHex, selectedPiece.Owner.ToDisplayName(), stateLabel);

            _inspectStatsText.text = string.Format(
                "HP: {0} / {1}    |    ATK: {2}    |    DEF: {3}\nAlcance de Ataque: {4}\nCustos: Desbloq {5} AP  |  Mover {6} AP  |  Atacar {7} AP",
                selectedPiece.CurrentHP,
                spec.MaxHP,
                spec.Attack,
                spec.Defense,
                spec.AttackRange,
                spec.UnlockCost,
                spec.MoveCost,
                spec.AttackCost);

            _inspectSummaryText.text = spec.RoleSummary;

            bool canShowUnlock = canActNow && !selectedPiece.IsUnlocked && !selectedPiece.HasActedThisTurn && selectedPiece.Owner == activeTeam;
            _unlockButton.gameObject.SetActive(canShowUnlock);
            if (canShowUnlock)
            {
                bool hasAp = activePlayer != null && activePlayer.CurrentAP >= spec.UnlockCost;
                _unlockButton.interactable = hasAp;
                _unlockButtonImage.color = hasAp ? ButtonUnlock : new Color(0.28f, 0.26f, 0.22f, 0.75f);
                _unlockButtonLabel.text = string.Format("Desbloquear Cápsula (-{0} AP)", spec.UnlockCost);
                _unlockButtonLabel.color = hasAp ? TextPrimary : TextMuted;
            }
        }

        private void RefreshEndTurnButton(MatchEngine engine, Color teamColor, bool canActNow)
        {
            bool isPlaying = engine.Phase == GamePhase.Playing;
            _endTurnRoot.SetActive(isPlaying);
            if (isPlaying)
            {
                _endTurnAccent.color = teamColor;
                _endTurnButton.interactable = canActNow;
                if (_endTurnLabel != null)
                {
                    _endTurnLabel.text = canActNow ? "Encerrar Turno" : "Turno do Oponente";
                }
            }
        }

        private void RefreshMultiplayerModal(SteamMatchSession steamSession)
        {
            if (_multiplayerRoot == null)
            {
                return;
            }

            bool steamAvailable = steamSession != null && steamSession.IsSteamAvailable;
            bool inLobby = steamSession != null && steamSession.IsInLobby;

            if (_mpSteamStatusText != null)
            {
                _mpSteamStatusText.color = steamAvailable ? new Color(0.37f, 0.92f, 0.83f, 1f) : new Color(0.98f, 0.75f, 0.24f, 1f);
                _mpSteamStatusText.text = steamSession != null ? steamSession.StatusMessage : "Steam indisponível (use Modo Local).";
            }

            if (_mpRetrySteamButton != null)
            {
                _mpRetrySteamButton.gameObject.SetActive(!steamAvailable);
            }

            if (_mpNotInLobbyRoot != null)
            {
                _mpNotInLobbyRoot.SetActive(!inLobby);
            }

            if (_mpInLobbyRoot != null)
            {
                _mpInLobbyRoot.SetActive(inLobby);
            }

            if (_mpCreateLobbyButton != null)
            {
                _mpCreateLobbyButton.interactable = steamAvailable;
            }

            if (_mpJoinLobbyButton != null)
            {
                _mpJoinLobbyButton.interactable = steamAvailable;
            }

            if (inLobby && steamSession != null)
            {
                _lastLobbyCode = steamSession.CurrentLobbyIdString ?? string.Empty;
                string role = steamSession.LocalTeam.ToDisplayName();
                string oppLine = steamSession.HasOpponent
                    ? "Conectado com " + steamSession.OpponentName + "!"
                    : "Aguardando seu colega entrar na sala...";

                if (_mpLobbyInfoText != null)
                {
                    _mpLobbyInfoText.text = "Código da Sala: " + _lastLobbyCode + "\nVocê é: " + role + "  •  " + oppLine;
                }

                if (_mpBackToMatchButton != null)
                {
                    _mpBackToMatchButton.gameObject.SetActive(steamSession.IsOnlineMatchActive);
                }
            }
        }

        private void RefreshGameOverModal(MatchEngine engine)
        {
            bool isGameOver = engine.Phase == GamePhase.GameOver;
            _gameOverRoot.SetActive(isGameOver);
            if (!isGameOver)
            {
                return;
            }

            PlayerTeam winner = engine.Winner ?? PlayerTeam.Player1;
            Color winColor = GetTeamColor(winner);
            _gameOverBorder.color = winColor;
            _gameOverWinnerText.color = winColor;
            _gameOverWinnerText.text = string.Format("VITÓRIA DO {0}!", winner.ToDisplayName().ToUpperInvariant());

            int p1Alive = engine.GetPieceCount(PlayerTeam.Player1, aliveOnly: true);
            int p2Alive = engine.GetPieceCount(PlayerTeam.Player2, aliveOnly: true);
            _gameOverStatsText.text = string.Format(
                "Partida encerrada no Turno {0}\nPeças vivas restantes — Jogador 1: {1}  |  Jogador 2: {2}",
                engine.TurnNumber,
                p1Alive,
                p2Alive);
        }

        private void OnAutoFillClicked()
        {
            if (_callbacks != null && _callbacks.OnAutoFillSetup != null)
            {
                _callbacks.OnAutoFillSetup.Invoke();
            }
        }

        private void OnConfirmSetupClicked()
        {
            if (_callbacks != null && _callbacks.OnConfirmSetup != null)
            {
                _callbacks.OnConfirmSetup.Invoke();
            }
        }

        private void OnUnlockClicked()
        {
            if (_callbacks != null && _callbacks.OnUnlockSelectedCapsule != null)
            {
                _callbacks.OnUnlockSelectedCapsule.Invoke();
            }
        }

        private void OnEndTurnClicked()
        {
            if (_callbacks != null && _callbacks.OnEndTurn != null)
            {
                _callbacks.OnEndTurn.Invoke();
            }
        }

        private void OnCameraAutoClicked()
        {
            _cameraAutoFlip = !_cameraAutoFlip;
            UpdateCameraAutoLabel();

            if (_callbacks != null && _callbacks.OnToggleCameraAutoFlip != null)
            {
                _callbacks.OnToggleCameraAutoFlip.Invoke();
            }
        }

        private void OnRestartClicked()
        {
            if (_callbacks != null && _callbacks.OnRestartMatch != null)
            {
                _callbacks.OnRestartMatch.Invoke();
            }
        }

        private void OnToggleMultiplayerMenuClicked()
        {
            SetMultiplayerMenuOpen(!IsMultiplayerMenuOpen);
        }

        private void OnCloseMultiplayerMenuClicked()
        {
            SetMultiplayerMenuOpen(false);
        }

        private void OnPlayLocalClicked()
        {
            SetMultiplayerMenuOpen(false);
            if (_callbacks != null && _callbacks.OnPlayLocalMode != null)
            {
                _callbacks.OnPlayLocalMode.Invoke();
            }
        }

        private void OnCreateSteamLobbyClicked()
        {
            if (_callbacks != null && _callbacks.OnCreateSteamLobby != null)
            {
                _callbacks.OnCreateSteamLobby.Invoke();
            }
        }

        private void OnJoinSteamLobbyClicked()
        {
            string code = _mpLobbyCodeInput != null ? _mpLobbyCodeInput.text : string.Empty;
            if (_callbacks != null && _callbacks.OnJoinSteamLobby != null)
            {
                _callbacks.OnJoinSteamLobby.Invoke(code);
            }
        }

        private void OnCopyLobbyCodeClicked()
        {
            if (!string.IsNullOrEmpty(_lastLobbyCode))
            {
                GUIUtility.systemCopyBuffer = _lastLobbyCode;
                if (_mpCopyCodeLabel != null)
                {
                    _mpCopyCodeLabel.text = "Código Copiado!";
                }
            }
        }

        private void OnInviteSteamFriendClicked()
        {
            if (_callbacks != null && _callbacks.OnInviteSteamFriend != null)
            {
                _callbacks.OnInviteSteamFriend.Invoke();
            }
        }

        private void OnLeaveSteamLobbyClicked()
        {
            if (_callbacks != null && _callbacks.OnLeaveSteamLobby != null)
            {
                _callbacks.OnLeaveSteamLobby.Invoke();
            }
        }

        private void OnRetrySteamClicked()
        {
            if (_callbacks != null && _callbacks.OnRetrySteamInit != null)
            {
                _callbacks.OnRetrySteamInit.Invoke();
            }
        }

        private void UpdateCameraAutoLabel()
        {
            if (_cameraAutoLabel != null)
            {
                _cameraAutoLabel.text = _cameraAutoFlip ? "Câmera Auto: ON" : "Câmera Auto: OFF";
            }
        }

        private static string FormatPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Setup:
                    return "FASE: POSICIONAMENTO DE CÁPSULAS";
                case GamePhase.Playing:
                    return "FASE: BATALHA";
                case GamePhase.GameOver:
                    return "FASE: FIM DE JOGO";
                default:
                    return "FASE";
            }
        }

        private static Color GetTeamColor(PlayerTeam team)
        {
            return team == PlayerTeam.Player1 ? P1Color : P2Color;
        }

        private static GameObject CreatePanel(string objectName, Transform parent, Color color, bool raycastTarget)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return go;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (tmp.font == null && TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            tmp.fontSize = fontSize;
            tmp.fontStyle = fontStyle;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Color backgroundColor,
            float fontSize,
            Action onClick,
            out TextMeshProUGUI labelComponent)
        {
            GameObject go = CreatePanel(objectName, parent, backgroundColor, raycastTarget: true);
            Button button = go.AddComponent<Button>();
            Image image = go.GetComponent<Image>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.disabledColor = new Color(0.60f, 0.60f, 0.60f, 0.70f);
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(delegate { onClick.Invoke(); });
            }

            labelComponent = CreateText("Label", go.transform, fontSize, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Center);
            StretchFill(labelComponent.rectTransform, 10f, 4f);
            labelComponent.text = label;

            return button;
        }

        private static TMP_InputField CreateInputField(string objectName, Transform parent, string placeholderText)
        {
            GameObject root = CreatePanel(objectName, parent, new Color(0.11f, 0.15f, 0.23f, 1f), raycastTarget: true);
            Image bg = root.GetComponent<Image>();

            GameObject viewport = new GameObject("TextArea");
            viewport.transform.SetParent(root.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            StretchFill(viewportRect, 12f, 6f);
            viewport.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = CreateText("Placeholder", viewportRect, 16f, FontStyles.Italic, new Color(0.55f, 0.62f, 0.74f, 0.85f), TextAlignmentOptions.MidlineLeft);
            StretchFill(placeholder.rectTransform, 0f, 0f);
            placeholder.text = placeholderText;

            TextMeshProUGUI text = CreateText("Text", viewportRect, 17f, FontStyles.Normal, TextPrimary, TextAlignmentOptions.MidlineLeft);
            StretchFill(text.rectTransform, 0f, 0f);

            TMP_InputField inputField = root.AddComponent<TMP_InputField>();
            inputField.targetGraphic = bg;
            inputField.textViewport = viewportRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.caretColor = P1Color;
            inputField.selectionColor = new Color(0.20f, 0.60f, 0.88f, 0.35f);
            return inputField;
        }

        private static void SetAnchoredBox(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void StretchFill(RectTransform rect, float horizontalInset, float verticalInset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static void AddLayoutElement(
            GameObject go,
            float minHeight = -1f,
            float preferredHeight = -1f,
            float minWidth = -1f,
            float preferredWidth = -1f,
            float flexibleWidth = -1f)
        {
            LayoutElement le = go.AddComponent<LayoutElement>();
            if (minHeight >= 0f) le.minHeight = minHeight;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            if (minWidth >= 0f) le.minWidth = minWidth;
            if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
        }
    }
}
