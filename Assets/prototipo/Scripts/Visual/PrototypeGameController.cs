using System.Collections.Generic;
using Prototipo.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Prototipo.Visual
{
    /// <summary>
    /// Ponto de entrada unico da cena do prototipo: conecta MatchEngine, BoardView,
    /// PieceView, CameraRigController e PrototypeHudView usando apenas o New Input System.
    /// </summary>
    public sealed class PrototypeGameController : MonoBehaviour
    {
        [SerializeField] private PrototypeConfig config;

        private readonly Dictionary<int, PieceView> _pieceViews = new Dictionary<int, PieceView>();
        private readonly List<BoardCoord> _cachedValidMoves = new List<BoardCoord>();
        private readonly List<BoardCoord> _cachedValidAttacks = new List<BoardCoord>();
        private readonly List<BoardCoord> _cachedSetupSpawnHighlight = new List<BoardCoord>();

        private BoardView _boardView;
        private CameraRigController _cameraRig;
        private PrototypeHudView _hudView;
        private SteamMatchSession _steamSession;
        private Camera _mainCamera;
        private Transform _piecesContainer;

        private int? _selectedPieceId;
        private BoardCoord? _hoveredCoord;
        private string _selectedSetupSpecId = "red";
        private string _statusToast = string.Empty;
        private bool _isOnlineMatch;
        private bool _isApplyingRemoteCommand;

        public MatchEngine Engine { get; private set; }

        /// <summary>
        /// Define o ScriptableObject de configuracao da partida (usado tambem pelo setup de cena no Editor).
        /// </summary>
        public void SetConfig(PrototypeConfig prototypeConfig)
        {
            config = prototypeConfig;
        }

        private void Start()
        {
            EnsureSceneSystems();
            StartNewMatch(isOnline: false, openMultiplayerMenu: true);
        }

        private void OnDestroy()
        {
            UnsubscribeEngineEvents();
            UnsubscribeSteamEvents();
        }

        private void Update()
        {
            if (Engine == null || _boardView == null)
            {
                return;
            }

            bool menuOpen = _hudView != null && _hudView.IsMultiplayerMenuOpen;
            if (_cameraRig != null)
            {
                _cameraRig.InputBlocked = menuOpen;
            }

            if (menuOpen)
            {
                return;
            }

            UpdateHoverAndPointerInput();
            UpdateKeyboardShortcuts();
        }

        /// <summary>
        /// Reinicia a partida limpando todas as pecas visuais e recriando o MatchEngine.
        /// Em modo Steam Online, sincroniza o reinicio com o oponente.
        /// </summary>
        public void RestartMatch()
        {
            if (_isOnlineMatch && _steamSession != null && _steamSession.IsOnlineMatchActive)
            {
                _steamSession.SendCommand(NetCommandMessage.Restart());
                StartNewMatch(isOnline: true, openMultiplayerMenu: false);
                return;
            }

            StartNewMatch(isOnline: false, openMultiplayerMenu: false);
        }

        private void StartNewMatch(bool isOnline, bool openMultiplayerMenu)
        {
            UnsubscribeEngineEvents();
            ClearAllPieceViews();

            _isOnlineMatch = isOnline;
            _selectedPieceId = null;
            _hoveredCoord = null;

            MatchConfigData configData = config != null
                ? config.ToConfigData()
                : MatchConfigData.CreateDefault();

            _selectedSetupSpecId = ResolveDefaultSpecId(configData);

            Engine = new MatchEngine(configData);
            SubscribeEngineEvents();

            _boardView.Build(Engine.Board);

            if (_isOnlineMatch && _steamSession != null)
            {
                _cameraRig.SetAutoFlip(false);
                _cameraRig.Initialize(_mainCamera, _boardView.GetBoardCenter(), _steamSession.LocalTeam);
            }
            else
            {
                _cameraRig.SetAutoFlip(true);
                _cameraRig.Initialize(_mainCamera, _boardView.GetBoardCenter(), Engine.ActiveTeam);
            }

            PrototypeHudCallbacks callbacks = new PrototypeHudCallbacks
            {
                OnSelectSetupCapsule = HandleSelectSetupCapsule,
                OnAutoFillSetup = HandleAutoFillSetup,
                OnConfirmSetup = HandleConfirmSetup,
                OnUnlockSelectedCapsule = HandleUnlockSelectedCapsule,
                OnEndTurn = HandleEndTurn,
                OnToggleCameraAutoFlip = HandleToggleCameraAutoFlip,
                OnRestartMatch = RestartMatch,
                OnPlayLocalMode = HandlePlayLocalMode,
                OnCreateSteamLobby = HandleCreateSteamLobby,
                OnJoinSteamLobby = HandleJoinSteamLobby,
                OnInviteSteamFriend = HandleInviteSteamFriend,
                OnLeaveSteamLobby = HandleLeaveSteamLobby,
                OnRetrySteamInit = HandleRetrySteamInit
            };

            _hudView.Initialize(Engine, callbacks);
            _hudView.SetCameraAutoFlipState(_cameraRig.AutoFlipEnabled);
            _hudView.SetMultiplayerMenuOpen(openMultiplayerMenu);

            if (_isOnlineMatch && _steamSession != null)
            {
                string role = _steamSession.LocalTeam.ToDisplayName();
                _statusToast = _steamSession.LocalTeam == Engine.ActiveTeam
                    ? "Partida Steam iniciada! Você é " + role + ": posicione suas cápsulas."
                    : "Partida Steam iniciada! Você é " + role + ". Aguarde " + _steamSession.OpponentName + " posicionar.";
                _hudView.AddLogEntry("Partida Steam iniciada vs " + _steamSession.OpponentName + " (" + role + ").");
            }
            else
            {
                _statusToast = Engine.ActiveTeam.ToDisplayName() + ": posicione suas cápsulas na zona destacada.";
                _hudView.AddLogEntry("Nova partida local iniciada — Fase de Posicionamento.");
            }

            RefreshAllViews();
        }

        private void EnsureSceneSystems()
        {
            // Garante Directional Light se a cena nao tiver uma
            Light existingLight = Object.FindAnyObjectByType<Light>();
            if (existingLight == null)
            {
                GameObject lightGo = new GameObject("Directional Light");
                Light dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.15f;
                dirLight.color = new Color(1.0f, 0.98f, 0.94f);
                lightGo.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            }

            // Garante Camera.main se a cena nao tiver uma
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                _mainCamera = Object.FindAnyObjectByType<Camera>();
                if (_mainCamera == null)
                {
                    GameObject camGo = new GameObject("Main Camera");
                    _mainCamera = camGo.AddComponent<Camera>();
                }

                _mainCamera.tag = "MainCamera";
            }

            if (_boardView == null)
            {
                _boardView = GetComponentInChildren<BoardView>();
                if (_boardView == null)
                {
                    GameObject boardGo = new GameObject("BoardView");
                    boardGo.transform.SetParent(transform, false);
                    _boardView = boardGo.AddComponent<BoardView>();
                }
            }

            if (_piecesContainer == null)
            {
                GameObject piecesGo = new GameObject("PiecesContainer");
                piecesGo.transform.SetParent(transform, false);
                _piecesContainer = piecesGo.transform;
            }

            if (_cameraRig == null)
            {
                _cameraRig = GetComponentInChildren<CameraRigController>();
                if (_cameraRig == null)
                {
                    GameObject rigGo = new GameObject("CameraRig");
                    rigGo.transform.SetParent(transform, false);
                    _cameraRig = rigGo.AddComponent<CameraRigController>();
                }
            }

            if (_hudView == null)
            {
                _hudView = GetComponentInChildren<PrototypeHudView>();
                if (_hudView == null)
                {
                    GameObject hudGo = new GameObject("PrototypeHudView");
                    hudGo.transform.SetParent(transform, false);
                    _hudView = hudGo.AddComponent<PrototypeHudView>();
                }
            }

            if (_steamSession == null)
            {
                _steamSession = GetComponentInChildren<SteamMatchSession>();
                if (_steamSession == null)
                {
                    GameObject steamGo = new GameObject("SteamMatchSession");
                    steamGo.transform.SetParent(transform, false);
                    _steamSession = steamGo.AddComponent<SteamMatchSession>();
                }

                SubscribeSteamEvents();
            }
        }

        private void UpdateHoverAndPointerInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool isOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (isOverUi)
            {
                if (_hoveredCoord.HasValue)
                {
                    _hoveredCoord = null;
                    RefreshBoardHighlights();
                }

                return;
            }

            BoardCoord? newHovered = RaycastBoardCoord(mouse.position.ReadValue());
            if (newHovered != _hoveredCoord)
            {
                _hoveredCoord = newHovered;
                RefreshBoardHighlights();
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                HandleLeftClick(newHovered);
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                HandleRightClick(newHovered);
            }
        }

        private void UpdateKeyboardShortcuts()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (Engine.Phase == GamePhase.Playing && kb.spaceKey.wasPressedThisFrame && IsLocalControlAllowed())
            {
                HandleEndTurn();
            }
        }

        private bool IsLocalControlAllowed()
        {
            if (!_isOnlineMatch)
            {
                return true;
            }

            return _steamSession != null
                && _steamSession.IsOnlineMatchActive
                && Engine != null
                && Engine.ActiveTeam == _steamSession.LocalTeam;
        }

        private void NotifyWaitingForOpponent()
        {
            string opp = _steamSession != null && !string.IsNullOrEmpty(_steamSession.OpponentName)
                ? _steamSession.OpponentName
                : "o oponente";
            _statusToast = "Aguarde a vez de " + opp + ".";
            RefreshHud();
        }

        private BoardCoord? RaycastBoardCoord(Vector2 screenPos)
        {
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
            {
                return null;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 250f))
            {
                if (_boardView.TryGetCoord(hit.collider.gameObject, out BoardCoord coord))
                {
                    return coord;
                }
            }

            return null;
        }

        private void HandleLeftClick(BoardCoord? clickedCoord)
        {
            if (Engine.Phase == GamePhase.Setup)
            {
                if (!clickedCoord.HasValue)
                {
                    return;
                }

                HandleSetupLeftClick(clickedCoord.Value);
                return;
            }

            if (Engine.Phase == GamePhase.Playing)
            {
                if (!clickedCoord.HasValue)
                {
                    ClearSelection();
                    return;
                }

                HandlePlayingLeftClick(clickedCoord.Value);
            }
        }

        private void HandleRightClick(BoardCoord? clickedCoord)
        {
            if (Engine.Phase == GamePhase.Setup)
            {
                if (!IsLocalControlAllowed())
                {
                    NotifyWaitingForOpponent();
                    return;
                }

                if (!clickedCoord.HasValue)
                {
                    return;
                }

                PlayerTeam team = Engine.ActiveTeam;
                ActionResult removeResult = Engine.RemoveSetupCapsule(team, clickedCoord.Value);
                if (removeResult.Success)
                {
                    SendIfOnline(NetCommandMessage.Remove(team, clickedCoord.Value));
                }

                if (!string.IsNullOrEmpty(removeResult.Message))
                {
                    _statusToast = removeResult.Message;
                    RefreshHud();
                }

                return;
            }

            if (Engine.Phase == GamePhase.Playing)
            {
                ClearSelection();
            }
        }

        private void HandleSetupLeftClick(BoardCoord coord)
        {
            if (!IsLocalControlAllowed())
            {
                NotifyWaitingForOpponent();
                return;
            }

            PlayerTeam team = Engine.ActiveTeam;
            PieceInstance existing = Engine.GetPieceAt(coord);
            if (existing != null && existing.Owner == team)
            {
                ActionResult removeResult = Engine.RemoveSetupCapsule(team, coord);
                if (removeResult.Success)
                {
                    SendIfOnline(NetCommandMessage.Remove(team, coord));
                }

                _statusToast = removeResult.Message ?? string.Empty;
                RefreshHud();
                return;
            }

            ActionResult placeResult = Engine.PlaceSetupCapsule(team, _selectedSetupSpecId, coord);
            if (placeResult.Success)
            {
                SendIfOnline(NetCommandMessage.Place(team, _selectedSetupSpecId, coord));
            }

            _statusToast = placeResult.Message ?? string.Empty;
            RefreshHud();
        }

        private void HandlePlayingLeftClick(BoardCoord coord)
        {
            if (!IsLocalControlAllowed())
            {
                NotifyWaitingForOpponent();
                return;
            }

            PieceInstance selected = GetSelectedPiece();
            PieceInstance clickedPiece = Engine.GetPieceAt(coord);
            PlayerTeam team = Engine.ActiveTeam;

            if (selected != null)
            {
                if (selected.IsUnlocked && !selected.HasActedThisTurn)
                {
                    if (_cachedValidMoves.Contains(coord))
                    {
                        ActionResult moveResult = Engine.MovePiece(team, selected.Id, coord);
                        if (moveResult.Success)
                        {
                            SendIfOnline(NetCommandMessage.Move(team, selected.Id, coord));
                            _selectedPieceId = null;
                        }

                        _statusToast = moveResult.Message ?? string.Empty;
                        RefreshAllViews();
                        return;
                    }

                    if (_cachedValidAttacks.Contains(coord))
                    {
                        ActionResult atkResult = Engine.AttackTarget(team, selected.Id, coord);
                        if (atkResult.Success)
                        {
                            SendIfOnline(NetCommandMessage.Attack(team, selected.Id, coord));
                            _selectedPieceId = null;
                        }

                        _statusToast = atkResult.Message ?? string.Empty;
                        RefreshAllViews();
                        return;
                    }

                    if (ContainsCoord(Engine.GetValidMoves(selected.Id, ignoreApCheck: true), coord))
                    {
                        ActionResult moveResult = Engine.MovePiece(team, selected.Id, coord);
                        if (moveResult.Success)
                        {
                            SendIfOnline(NetCommandMessage.Move(team, selected.Id, coord));
                            _selectedPieceId = null;
                        }

                        _statusToast = moveResult.Message ?? string.Empty;
                        RefreshAllViews();
                        return;
                    }

                    if (ContainsCoord(Engine.GetValidAttacks(selected.Id, ignoreApCheck: true), coord))
                    {
                        ActionResult atkResult = Engine.AttackTarget(team, selected.Id, coord);
                        if (atkResult.Success)
                        {
                            SendIfOnline(NetCommandMessage.Attack(team, selected.Id, coord));
                            _selectedPieceId = null;
                        }

                        _statusToast = atkResult.Message ?? string.Empty;
                        RefreshAllViews();
                        return;
                    }
                }
                else if (!selected.HasActedThisTurn && clickedPiece != null && clickedPiece.Id == selected.Id)
                {
                    // Segundo clique na propria capsula fechada funciona como atalho para desbloquear.
                    HandleUnlockSelectedCapsule();
                    return;
                }
            }

            if (clickedPiece != null && clickedPiece.IsAlive && clickedPiece.Owner == team)
            {
                if (clickedPiece.HasActedThisTurn)
                {
                    _selectedPieceId = null;
                    _statusToast = clickedPiece.Spec.DisplayName + " já agiu neste turno (aguarde o próximo turno).";
                    RefreshAllViews();
                    return;
                }

                SelectPiece(clickedPiece);
                return;
            }

            if (clickedPiece != null && clickedPiece.IsAlive && clickedPiece.Owner != team)
            {
                if (!clickedPiece.IsUnlocked && _isOnlineMatch)
                {
                    _statusToast = clickedPiece.Owner.ToDisplayName() + ": Cápsula Fechada (identidade oculta até desencapsular).";
                }
                else
                {
                    string label = clickedPiece.IsUnlocked ? clickedPiece.Spec.DisplayName : "Cápsula Fechada";
                    _statusToast = clickedPiece.Owner.ToDisplayName() + ": " + label + " (HP " + clickedPiece.CurrentHP + "/" + clickedPiece.Spec.MaxHP + ").";
                }

                RefreshHud();
                return;
            }

            ClearSelection();
        }

        private void SelectPiece(PieceInstance piece)
        {
            _selectedPieceId = piece.Id;

            if (!piece.IsUnlocked)
            {
                _statusToast = "Cápsula selecionada. Clique novamente ou use Desbloquear (-" + piece.Spec.UnlockCost + " AP).";
            }
            else
            {
                int currentAp = Engine.GetPlayer(Engine.ActiveTeam).CurrentAP;
                if (currentAp < piece.Spec.MoveCost && currentAp < piece.Spec.AttackCost)
                {
                    _statusToast = piece.Spec.DisplayName + " selecionado (AP insuficiente para agir).";
                }
                else
                {
                    _statusToast = piece.Spec.DisplayName + " selecionado. Ciano = Mover | Vermelho = Atacar.";
                }
            }

            RefreshAllViews();
        }

        private void ClearSelection()
        {
            if (!_selectedPieceId.HasValue)
            {
                return;
            }

            _selectedPieceId = null;
            _statusToast = string.Empty;
            RefreshAllViews();
        }

        private PieceInstance GetSelectedPiece()
        {
            if (!_selectedPieceId.HasValue || Engine == null)
            {
                return null;
            }

            PieceInstance piece = Engine.GetPiece(_selectedPieceId.Value);
            if (piece == null || !piece.IsAlive)
            {
                _selectedPieceId = null;
                return null;
            }

            return piece;
        }

        // Callbacks do HUD
        private void HandleSelectSetupCapsule(string specId)
        {
            if (string.IsNullOrEmpty(specId))
            {
                return;
            }

            _selectedSetupSpecId = specId;
            PieceSpec spec = Engine != null ? Engine.Config.FindSpec(specId) : null;
            _statusToast = spec != null
                ? "Selecionado: " + spec.DisplayName + ". Clique na zona de spawn para posicionar."
                : string.Empty;
            RefreshHud();
        }

        private void HandleAutoFillSetup()
        {
            if (Engine == null || !IsLocalControlAllowed())
            {
                return;
            }

            PlayerTeam team = Engine.ActiveTeam;
            ActionResult result = Engine.AutoFillSetupForTeam(team);
            if (result.Success)
            {
                SendIfOnline(NetCommandMessage.AutoFill(team));
            }

            _statusToast = result.Message ?? string.Empty;
            RefreshAllViews();
        }

        private void HandleConfirmSetup()
        {
            if (Engine == null || !IsLocalControlAllowed())
            {
                return;
            }

            PlayerTeam team = Engine.ActiveTeam;
            ActionResult result = Engine.ConfirmSetup(team);
            if (result.Success)
            {
                SendIfOnline(NetCommandMessage.Confirm(team));
            }

            _statusToast = result.Message ?? string.Empty;
            RefreshAllViews();
        }

        private void HandleUnlockSelectedCapsule()
        {
            PieceInstance selected = GetSelectedPiece();
            if (Engine == null || selected == null || !IsLocalControlAllowed())
            {
                return;
            }

            PlayerTeam team = Engine.ActiveTeam;
            ActionResult result = Engine.UnlockCapsule(team, selected.Id);
            if (result.Success)
            {
                SendIfOnline(NetCommandMessage.Unlock(team, selected.Id));
                _selectedPieceId = null;
            }

            _statusToast = result.Message ?? string.Empty;
            RefreshAllViews();
        }

        private void HandleEndTurn()
        {
            if (Engine == null || !IsLocalControlAllowed())
            {
                return;
            }

            PlayerTeam team = Engine.ActiveTeam;
            ActionResult result = Engine.EndTurn(team);
            if (result.Success)
            {
                SendIfOnline(NetCommandMessage.EndTurn(team));
            }

            _statusToast = result.Message ?? string.Empty;
            RefreshAllViews();
        }

        // Callbacks do Steam / Multiplayer
        private void HandlePlayLocalMode()
        {
            if (_steamSession != null && _steamSession.IsInLobby)
            {
                _steamSession.LeaveLobby(silent: true);
            }

            if (_isOnlineMatch)
            {
                StartNewMatch(isOnline: false, openMultiplayerMenu: false);
            }
            else
            {
                _hudView.SetMultiplayerMenuOpen(false);
                RefreshAllViews();
            }
        }

        private void HandleCreateSteamLobby()
        {
            if (_steamSession != null)
            {
                _steamSession.CreateLobby();
            }
        }

        private void HandleJoinSteamLobby(string code)
        {
            if (_steamSession != null)
            {
                _steamSession.JoinLobbyByCode(code);
            }
        }

        private void HandleInviteSteamFriend()
        {
            if (_steamSession != null)
            {
                _steamSession.OpenInviteOverlay();
            }
        }

        private void HandleLeaveSteamLobby()
        {
            if (_steamSession != null)
            {
                _steamSession.LeaveLobby();
            }

            StartNewMatch(isOnline: false, openMultiplayerMenu: true);
        }

        private void HandleRetrySteamInit()
        {
            if (_steamSession != null)
            {
                _steamSession.InitializeSteam();
            }
        }

        private void SendIfOnline(NetCommandMessage message)
        {
            if (_isOnlineMatch && !_isApplyingRemoteCommand && _steamSession != null)
            {
                _steamSession.SendCommand(message);
            }
        }

        private void SubscribeSteamEvents()
        {
            if (_steamSession == null)
            {
                return;
            }

            _steamSession.OnSessionChanged += HandleSteamSessionChanged;
            _steamSession.OnRemoteCommandReceived += HandleRemoteCommandReceived;
            _steamSession.OnMatchStartRequestedByHost += HandleSteamMatchStart;
            _steamSession.OnSessionNotice += HandleSteamNotice;
        }

        private void UnsubscribeSteamEvents()
        {
            if (_steamSession == null)
            {
                return;
            }

            _steamSession.OnSessionChanged -= HandleSteamSessionChanged;
            _steamSession.OnRemoteCommandReceived -= HandleRemoteCommandReceived;
            _steamSession.OnMatchStartRequestedByHost -= HandleSteamMatchStart;
            _steamSession.OnSessionNotice -= HandleSteamNotice;
        }

        private void HandleSteamSessionChanged()
        {
            RefreshAllViews();
        }

        private void HandleSteamNotice(string notice)
        {
            _statusToast = notice ?? string.Empty;
            if (_hudView != null)
            {
                _hudView.AddLogEntry(notice);
            }

            RefreshHud();
        }

        private void HandleSteamMatchStart()
        {
            StartNewMatch(isOnline: true, openMultiplayerMenu: false);
        }

        private void HandleRemoteCommandReceived(NetCommandMessage cmd)
        {
            if (cmd == null || Engine == null)
            {
                return;
            }

            _isApplyingRemoteCommand = true;
            try
            {
                if (cmd.type == NetCommandMessage.TypeRestart)
                {
                    StartNewMatch(isOnline: true, openMultiplayerMenu: false);
                    _statusToast = "Partida online reiniciada pelo oponente.";
                    RefreshAllViews();
                    return;
                }

                ActionResult result = ActionResult.Fail("Comando remoto desconhecido.");
                switch (cmd.type)
                {
                    case NetCommandMessage.TypePlace:
                        result = Engine.PlaceSetupCapsule(cmd.TeamEnum, cmd.specId, cmd.Coord);
                        break;
                    case NetCommandMessage.TypeRemove:
                        result = Engine.RemoveSetupCapsule(cmd.TeamEnum, cmd.Coord);
                        break;
                    case NetCommandMessage.TypeAutoFill:
                        result = Engine.AutoFillSetupForTeam(cmd.TeamEnum);
                        break;
                    case NetCommandMessage.TypeConfirm:
                        result = Engine.ConfirmSetup(cmd.TeamEnum);
                        break;
                    case NetCommandMessage.TypeUnlock:
                        result = Engine.UnlockCapsule(cmd.TeamEnum, cmd.pieceId);
                        break;
                    case NetCommandMessage.TypeMove:
                        result = Engine.MovePiece(cmd.TeamEnum, cmd.pieceId, cmd.Coord);
                        break;
                    case NetCommandMessage.TypeAttack:
                        result = Engine.AttackTarget(cmd.TeamEnum, cmd.pieceId, cmd.Coord);
                        break;
                    case NetCommandMessage.TypeEndTurn:
                        result = Engine.EndTurn(cmd.TeamEnum);
                        break;
                }

                if (!result.Success)
                {
                    Debug.LogWarning("[PrototypeGameController] Falha ao aplicar comando remoto '" + cmd.type + "': " + result.Message);
                }

                if (_steamSession != null && Engine.ActiveTeam == _steamSession.LocalTeam && Engine.Phase != GamePhase.GameOver)
                {
                    _statusToast = "Sua vez! (" + _steamSession.LocalTeam.ToDisplayName() + ")";
                }
                else if (cmd.type == NetCommandMessage.TypePlace && result.Success)
                {
                    _statusToast = "Oponente posicionou uma cápsula fechada.";
                }
                else if (result.Success && !string.IsNullOrEmpty(result.Message))
                {
                    _statusToast = result.Message;
                }

                RefreshAllViews();
            }
            finally
            {
                _isApplyingRemoteCommand = false;
            }
        }

        private void HandleToggleCameraAutoFlip()
        {
            if (_cameraRig == null || Engine == null)
            {
                return;
            }

            bool enabled = _cameraRig.ToggleAutoFlip(Engine.ActiveTeam);
            if (_hudView != null)
            {
                _hudView.SetCameraAutoFlipState(enabled);
            }

            _statusToast = enabled ? "Câmera Auto-Flip ativada." : "Câmera Auto-Flip desativada (use Q/E para girar).";
            RefreshHud();
        }

        // Eventos do MatchEngine
        private void SubscribeEngineEvents()
        {
            if (Engine == null)
            {
                return;
            }

            Engine.OnStateChanged += HandleEngineStateChanged;
            Engine.OnCapsulePlaced += HandleCapsulePlaced;
            Engine.OnCapsuleRemoved += HandleCapsuleRemoved;
            Engine.OnCapsuleUnlocked += HandleCapsuleUnlocked;
            Engine.OnPieceMoved += HandlePieceMoved;
            Engine.OnPieceAttacked += HandlePieceAttacked;
            Engine.OnTurnChanged += HandleTurnChanged;
            Engine.OnPhaseChanged += HandlePhaseChanged;
            Engine.OnGameOver += HandleGameOver;
            Engine.OnLogMessage += HandleLogMessage;
        }

        private void UnsubscribeEngineEvents()
        {
            if (Engine == null)
            {
                return;
            }

            Engine.OnStateChanged -= HandleEngineStateChanged;
            Engine.OnCapsulePlaced -= HandleCapsulePlaced;
            Engine.OnCapsuleRemoved -= HandleCapsuleRemoved;
            Engine.OnCapsuleUnlocked -= HandleCapsuleUnlocked;
            Engine.OnPieceMoved -= HandlePieceMoved;
            Engine.OnPieceAttacked -= HandlePieceAttacked;
            Engine.OnTurnChanged -= HandleTurnChanged;
            Engine.OnPhaseChanged -= HandlePhaseChanged;
            Engine.OnGameOver -= HandleGameOver;
            Engine.OnLogMessage -= HandleLogMessage;
        }

        private void HandleEngineStateChanged()
        {
            RefreshAllViews();
        }

        private void HandleCapsulePlaced(PieceInstance piece)
        {
            if (piece == null)
            {
                return;
            }

            GameObject go = new GameObject("Piece_" + piece.Id + "_" + piece.Owner + "_" + piece.Spec.Id);
            go.transform.SetParent(_piecesContainer, false);

            PieceView view = go.AddComponent<PieceView>();
            bool hideClosedIdentity = _isOnlineMatch && _steamSession != null && piece.Owner != _steamSession.LocalTeam;
            view.Initialize(piece, _boardView.GetTileTopPosition(piece.Position), hideClosedIdentity);
            _pieceViews[piece.Id] = view;
        }

        private void HandleCapsuleRemoved(PieceInstance piece)
        {
            if (piece == null)
            {
                return;
            }

            if (_selectedPieceId.HasValue && _selectedPieceId.Value == piece.Id)
            {
                _selectedPieceId = null;
            }

            if (_pieceViews.TryGetValue(piece.Id, out PieceView view))
            {
                _pieceViews.Remove(piece.Id);
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }
        }

        private void HandleCapsuleUnlocked(CapsuleUnlockedData data)
        {
            PieceInstance piece = Engine.GetPiece(data.PieceId);
            if (_pieceViews.TryGetValue(data.PieceId, out PieceView view) && view != null && piece != null)
            {
                view.PlayUnlockAnimation(piece);
            }
        }

        private void HandlePieceMoved(PieceMovedData data)
        {
            if (_pieceViews.TryGetValue(data.PieceId, out PieceView view) && view != null)
            {
                Vector3 fromWorld = _boardView.GetTileTopPosition(data.From);
                Vector3 toWorld = _boardView.GetTileTopPosition(data.To);
                view.PlayMoveAnimation(fromWorld, toWorld);
            }
        }

        private void HandlePieceAttacked(PieceAttackedData data)
        {
            if (_pieceViews.TryGetValue(data.AttackerId, out PieceView attackerView) && attackerView != null)
            {
                attackerView.PlayAttackBump(_boardView.GetTileTopPosition(data.TargetPos));
            }

            if (_pieceViews.TryGetValue(data.TargetId, out PieceView targetView) && targetView != null)
            {
                if (data.TargetDied)
                {
                    _pieceViews.Remove(data.TargetId);
                    if (_selectedPieceId.HasValue && _selectedPieceId.Value == data.TargetId)
                    {
                        _selectedPieceId = null;
                    }

                    targetView.PlayDeathAndDestroy();
                }
                else
                {
                    PieceInstance target = Engine.GetPiece(data.TargetId);
                    targetView.PlayHitFlash(target);
                }
            }
        }

        private void HandleTurnChanged(PlayerTeam newTeam, int turnNumber)
        {
            _selectedPieceId = null;
            if (_cameraRig != null)
            {
                _cameraRig.SetTeamOrientation(newTeam);
            }
        }

        private void HandlePhaseChanged(GamePhase newPhase)
        {
            _selectedPieceId = null;
            if (_cameraRig != null && Engine != null)
            {
                _cameraRig.SetTeamOrientation(Engine.ActiveTeam);
            }
        }

        private void HandleGameOver(PlayerTeam winner)
        {
            _selectedPieceId = null;
            _statusToast = "FIM DE JOGO! " + winner.ToDisplayName() + " venceu a partida!";
        }

        private void HandleLogMessage(string message)
        {
            if (_hudView == null)
            {
                return;
            }

            if (_isOnlineMatch
                && _steamSession != null
                && Engine != null
                && Engine.Phase == GamePhase.Setup
                && Engine.ActiveTeam != _steamSession.LocalTeam
                && message != null
                && message.Contains("posicionou cápsula"))
            {
                _hudView.AddLogEntry(Engine.ActiveTeam.ToDisplayName() + " posicionou uma cápsula fechada.");
                return;
            }

            _hudView.AddLogEntry(message);
        }

        private void RefreshAllViews()
        {
            RefreshPieceSelectionVisuals();
            RefreshBoardHighlights();
            RefreshHud();
        }

        private void RefreshPieceSelectionVisuals()
        {
            foreach (KeyValuePair<int, PieceView> kvp in _pieceViews)
            {
                if (kvp.Value != null)
                {
                    PieceInstance piece = Engine != null ? Engine.GetPiece(kvp.Key) : null;
                    kvp.Value.SetActedThisTurn(piece);
                    bool isSelected = _selectedPieceId.HasValue
                        && kvp.Key == _selectedPieceId.Value
                        && (piece == null || !piece.HasActedThisTurn);
                    kvp.Value.SetSelected(isSelected);
                }
            }
        }

        private void RefreshBoardHighlights()
        {
            if (_boardView == null || Engine == null)
            {
                return;
            }

            _cachedValidMoves.Clear();
            _cachedValidAttacks.Clear();
            _cachedSetupSpawnHighlight.Clear();

            BoardCoord? selectedCoord = null;

            if (Engine.Phase == GamePhase.Setup && IsLocalControlAllowed())
            {
                IReadOnlyList<BoardCoord> spawnCoords = Engine.Board.GetSpawnCoords(Engine.ActiveTeam);
                for (int i = 0; i < spawnCoords.Count; i++)
                {
                    BoardTile tile = Engine.Board.GetTile(spawnCoords[i]);
                    if (tile != null && tile.IsWalkable && !tile.IsOccupied)
                    {
                        _cachedSetupSpawnHighlight.Add(spawnCoords[i]);
                    }
                }
            }
            else if (Engine.Phase == GamePhase.Playing)
            {
                PieceInstance selected = GetSelectedPiece();
                if (selected != null)
                {
                    selectedCoord = selected.Position;
                    if (selected.IsUnlocked && selected.Owner == Engine.ActiveTeam)
                    {
                        IReadOnlyList<BoardCoord> moves = Engine.GetValidMoves(selected.Id);
                        for (int i = 0; i < moves.Count; i++)
                        {
                            _cachedValidMoves.Add(moves[i]);
                        }

                        IReadOnlyList<BoardCoord> attacks = Engine.GetValidAttacks(selected.Id);
                        for (int i = 0; i < attacks.Count; i++)
                        {
                            _cachedValidAttacks.Add(attacks[i]);
                        }
                    }
                }
            }

            _boardView.SetHighlights(
                selectedCoord,
                _cachedValidMoves,
                _cachedValidAttacks,
                _cachedSetupSpawnHighlight,
                _hoveredCoord);
        }

        private void RefreshHud()
        {
            if (_hudView == null || Engine == null)
            {
                return;
            }

            _hudView.Refresh(Engine, GetSelectedPiece(), _selectedSetupSpecId, _statusToast, _steamSession);
        }

        private void ClearAllPieceViews()
        {
            foreach (KeyValuePair<int, PieceView> kvp in _pieceViews)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }

            _pieceViews.Clear();
        }

        private static bool ContainsCoord(IReadOnlyList<BoardCoord> list, BoardCoord target)
        {
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveDefaultSpecId(MatchConfigData configData)
        {
            if (configData != null && configData.Catalog != null && configData.Catalog.Count > 0)
            {
                PieceSpec red = configData.FindSpec("red");
                return red != null ? red.Id : configData.Catalog[0].Id;
            }

            return "red";
        }
    }
}
