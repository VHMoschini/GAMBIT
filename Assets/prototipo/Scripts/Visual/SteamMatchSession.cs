using System;
using System.Text;
using Prototipo.Core;
using UnityEngine;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Prototipo.Visual
{
    /// <summary>
    /// Gerencia inicializacao do Steamworks.NET (AppID 480 / Spacewar via steam_appid.txt),
    /// criacao/entrada de Lobby Steam (por convite da lista de amigos ou codigo da sala)
    /// e transporte confiavel de comandos de turno via mensagens do Lobby.
    /// </summary>
    public sealed class SteamMatchSession : MonoBehaviour
    {
        private const string MessagePrefix = "GAMBIT_CMD:";
        private const string LobbyGameKey = "game";
        private const string LobbyGameValue = "GAMBIT_PROTOTIPO";
        private const string LobbyHostKey = "host";

        public bool IsSteamAvailable { get; private set; }
        public bool IsInLobby { get; private set; }
        public bool IsHost { get; private set; }
        public bool HasOpponent { get; private set; }
        public bool IsOnlineMatchActive => IsInLobby && HasOpponent;
        public PlayerTeam LocalTeam => IsHost ? PlayerTeam.Player1 : PlayerTeam.Player2;

        public string LocalPlayerName { get; private set; } = "Local";
        public string OpponentName { get; private set; } = string.Empty;
        public string CurrentLobbyIdString { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = "Steam não inicializada.";

        public event Action OnSessionChanged;
#pragma warning disable 0067
        public event Action<NetCommandMessage> OnRemoteCommandReceived;
        public event Action OnMatchStartRequestedByHost;
#pragma warning restore 0067
        public event Action<string> OnSessionNotice;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        private int _outgoingSequence;
        private readonly byte[] _recvBuffer = new byte[4096];
        private CSteamID _currentLobbyId = CSteamID.Nil;
        private bool _ownsSteamInit;

        private CallResult<LobbyCreated_t> _lobbyCreatedCallResult;
        private Callback<LobbyEnter_t> _lobbyEnteredCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
        private Callback<LobbyChatMsg_t> _lobbyChatMsgCallback;
        private Callback<GameLobbyJoinRequested_t> _joinRequestedCallback;
#endif

        private void Awake()
        {
            InitializeSteam();
        }

        private void Update()
        {
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (IsSteamAvailable)
            {
                SteamAPI.RunCallbacks();
            }
#endif
        }

        private void OnDestroy()
        {
            ShutdownSteam();
        }

        private void OnApplicationQuit()
        {
            ShutdownSteam();
        }

        public void InitializeSteam()
        {
            if (IsSteamAvailable)
            {
                return;
            }

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            try
            {
                if (!Packsize.Test())
                {
                    StatusMessage = "Steamworks.NET: erro de Packsize para esta plataforma.";
                    Debug.LogError("[SteamMatchSession] " + StatusMessage);
                    RaiseSessionChanged();
                    return;
                }

                if (!DllCheck.Test())
                {
                    StatusMessage = "Steamworks.NET: steam_api64.dll incompatível ou ausente.";
                    Debug.LogError("[SteamMatchSession] " + StatusMessage);
                    RaiseSessionChanged();
                    return;
                }

                if (!SteamAPI.Init())
                {
                    StatusMessage = "Steam fechada ou steam_appid.txt ausente. Abra a Steam para jogar online (modo Local disponível).";
                    Debug.LogWarning("[SteamMatchSession] " + StatusMessage);
                    RaiseSessionChanged();
                    return;
                }

                _ownsSteamInit = true;
                IsSteamAvailable = true;
                Application.runInBackground = true;
                LocalPlayerName = SteamFriends.GetPersonaName();
                StatusMessage = "Steam conectada como " + LocalPlayerName + ".";

                RegisterCallbacks();
                CheckCommandLineAutoJoin();
                RaiseSessionChanged();
            }
            catch (Exception ex)
            {
                IsSteamAvailable = false;
                StatusMessage = "Falha ao inicializar Steam: " + ex.Message;
                Debug.LogWarning("[SteamMatchSession] " + StatusMessage);
                RaiseSessionChanged();
            }
#else
            IsSteamAvailable = false;
            StatusMessage = "Pacote Steamworks.NET não carregado ainda (aguarde o Unity importar o pacote ou use Modo Local).";
            RaiseSessionChanged();
#endif
        }

        public void CreateLobby()
        {
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (!IsSteamAvailable)
            {
                InitializeSteam();
                if (!IsSteamAvailable)
                {
                    EmitNotice(StatusMessage);
                    return;
                }
            }

            if (IsInLobby)
            {
                LeaveLobby(silent: true);
            }

            StatusMessage = "Criando sala na Steam...";
            RaiseSessionChanged();

            SteamAPICall_t handle = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
            _lobbyCreatedCallResult.Set(handle);
#else
            EmitNotice("Steamworks.NET não está ativo neste build.");
#endif
        }

        public void JoinLobbyByCode(string lobbyIdText)
        {
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (!IsSteamAvailable)
            {
                InitializeSteam();
                if (!IsSteamAvailable)
                {
                    EmitNotice(StatusMessage);
                    return;
                }
            }

            string trimmed = lobbyIdText != null ? lobbyIdText.Trim() : string.Empty;
            if (!ulong.TryParse(trimmed, out ulong rawId) || rawId == 0)
            {
                StatusMessage = "Código de sala inválido. Cole o número completo da sala Steam.";
                EmitNotice(StatusMessage);
                RaiseSessionChanged();
                return;
            }

            JoinLobbyInternal(new CSteamID(rawId));
#else
            EmitNotice("Steamworks.NET não está ativo neste build.");
#endif
        }

        public void OpenInviteOverlay()
        {
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (!IsSteamAvailable || !IsInLobby || _currentLobbyId == CSteamID.Nil)
            {
                EmitNotice("Crie uma sala Steam primeiro para convidar um amigo.");
                return;
            }

            SteamFriends.ActivateGameOverlayInviteDialog(_currentLobbyId);
            EmitNotice("Overlay de convite da Steam aberto (ou copie o código da sala).");
#else
            EmitNotice("Steamworks.NET não está ativo neste build.");
#endif
        }

        public void LeaveLobby(bool silent = false)
        {
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (IsInLobby && _currentLobbyId != CSteamID.Nil && IsSteamAvailable)
            {
                SteamMatchmaking.LeaveLobby(_currentLobbyId);
            }

            _currentLobbyId = CSteamID.Nil;
#endif
            IsInLobby = false;
            IsHost = false;
            HasOpponent = false;
            OpponentName = string.Empty;
            CurrentLobbyIdString = string.Empty;
            StatusMessage = IsSteamAvailable
                ? "Steam conectada como " + LocalPlayerName + "."
                : "Modo Local.";

            if (!silent)
            {
                EmitNotice("Você saiu da sala Steam.");
            }

            RaiseSessionChanged();
        }

        public bool SendCommand(NetCommandMessage message)
        {
            if (message == null)
            {
                return false;
            }

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (!IsSteamAvailable || !IsInLobby || _currentLobbyId == CSteamID.Nil)
            {
                return false;
            }

            message.seq = ++_outgoingSequence;
            string payload = MessagePrefix + JsonUtility.ToJson(message);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            bool sent = SteamMatchmaking.SendLobbyChatMsg(_currentLobbyId, bytes, bytes.Length);
            if (!sent)
            {
                Debug.LogWarning("[SteamMatchSession] Falha ao enviar mensagem pelo Lobby Steam: " + message.type);
            }

            return sent;
#else
            return false;
#endif
        }

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        private void RegisterCallbacks()
        {
            _lobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(HandleLobbyCreated);
            _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(HandleLobbyEntered);
            _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(HandleLobbyChatUpdate);
            _lobbyChatMsgCallback = Callback<LobbyChatMsg_t>.Create(HandleLobbyChatMessage);
            _joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(HandleGameLobbyJoinRequested);
        }

        private void CheckCommandLineAutoJoin()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase))
                {
                    if (ulong.TryParse(args[i + 1], out ulong lobbyId) && lobbyId != 0)
                    {
                        Debug.Log("[SteamMatchSession] Auto-entrando no lobby via argumento Steam: " + lobbyId);
                        JoinLobbyInternal(new CSteamID(lobbyId));
                    }

                    break;
                }
            }
        }

        private void JoinLobbyInternal(CSteamID lobbyId)
        {
            if (IsInLobby)
            {
                LeaveLobby(silent: true);
            }

            StatusMessage = "Entrando na sala Steam " + lobbyId.m_SteamID + "...";
            RaiseSessionChanged();
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        private void HandleLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                StatusMessage = "Não foi possível criar a sala Steam (" + result.m_eResult + ").";
                EmitNotice(StatusMessage);
                RaiseSessionChanged();
                return;
            }

            _currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentLobbyIdString = result.m_ulSteamIDLobby.ToString();
            IsInLobby = true;
            IsHost = true;
            HasOpponent = false;
            OpponentName = string.Empty;

            SteamMatchmaking.SetLobbyData(_currentLobbyId, LobbyGameKey, LobbyGameValue);
            SteamMatchmaking.SetLobbyData(_currentLobbyId, LobbyHostKey, LocalPlayerName);

            StatusMessage = "Sala criada! Convide seu amigo pela Steam ou passe o código: " + CurrentLobbyIdString;
            EmitNotice("Sala Steam criada! Aguardando Jogador 2...");
            RaiseSessionChanged();
        }

        private void HandleLobbyEntered(LobbyEnter_t result)
        {
            if (result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                StatusMessage = "Falha ao entrar na sala Steam (código " + result.m_EChatRoomEnterResponse + ").";
                EmitNotice(StatusMessage);
                RaiseSessionChanged();
                return;
            }

            _currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentLobbyIdString = result.m_ulSteamIDLobby.ToString();
            IsInLobby = true;

            CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentLobbyId);
            IsHost = owner == SteamUser.GetSteamID();

            RefreshLobbyMembers();

            if (!IsHost)
            {
                StatusMessage = HasOpponent
                    ? "Conectado à sala de " + OpponentName + "! Sincronizando partida..."
                    : "Conectado à sala! Aguardando Host...";
                RaiseSessionChanged();

                // Handshake: cliente avisa que entrou e ja esta ouvindo LobbyChatMsg_t.
                SendCommand(NetCommandMessage.ClientReady());
            }
            else
            {
                RaiseSessionChanged();
            }
        }

        private void HandleLobbyChatUpdate(LobbyChatUpdate_t update)
        {
            if (!IsInLobby || update.m_ulSteamIDLobby != _currentLobbyId.m_SteamID)
            {
                return;
            }

            bool previouslyHadOpponent = HasOpponent;
            string previousName = OpponentName;

            // Caso o host saia, a Steam promove o outro membro a dono — verificamos owner atual.
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentLobbyId);
            IsHost = owner == SteamUser.GetSteamID();

            RefreshLobbyMembers();

            uint flags = update.m_rgfChatMemberStateChange;
            bool entered = (flags & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0;
            bool left = (flags & ((uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft
                | (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
                | (uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked
                | (uint)EChatMemberStateChange.k_EChatMemberStateChangeBanned)) != 0;

            if (entered && HasOpponent)
            {
                StatusMessage = OpponentName + " entrou na sala! Iniciando partida...";
                EmitNotice(OpponentName + " entrou na sala Steam!");
            }
            else if (left && previouslyHadOpponent && !HasOpponent)
            {
                string who = string.IsNullOrEmpty(previousName) ? "O oponente" : previousName;
                StatusMessage = who + " saiu da sala. Aguardando novo jogador...";
                EmitNotice(who + " desconectou da sala.");
            }

            RaiseSessionChanged();
        }

        private void HandleLobbyChatMessage(LobbyChatMsg_t msg)
        {
            if (!IsInLobby || msg.m_ulSteamIDLobby != _currentLobbyId.m_SteamID)
            {
                return;
            }

            int bytesRead = SteamMatchmaking.GetLobbyChatEntry(
                _currentLobbyId,
                (int)msg.m_iChatID,
                out CSteamID senderId,
                _recvBuffer,
                _recvBuffer.Length,
                out EChatEntryType _);

            if (bytesRead <= 0)
            {
                return;
            }

            // Ignora mensagens enviadas por nos mesmos (ja aplicadas localmente).
            if (senderId == SteamUser.GetSteamID())
            {
                return;
            }

            string raw = Encoding.UTF8.GetString(_recvBuffer, 0, bytesRead).TrimEnd('\0');
            if (!raw.StartsWith(MessagePrefix, StringComparison.Ordinal))
            {
                return;
            }

            string json = raw.Substring(MessagePrefix.Length);
            NetCommandMessage command;
            try
            {
                command = JsonUtility.FromJson<NetCommandMessage>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SteamMatchSession] JSON inválido recebido da Steam: " + ex.Message);
                return;
            }

            if (command == null || string.IsNullOrEmpty(command.type))
            {
                return;
            }

            if (command.type == NetCommandMessage.TypeClientReady && IsHost)
            {
                RefreshLobbyMembers();
                StatusMessage = "Partida online com " + OpponentName + " (Você = Jogador 1).";
                SendCommand(NetCommandMessage.SyncStart());
                RaiseSessionChanged();
                if (OnMatchStartRequestedByHost != null)
                {
                    OnMatchStartRequestedByHost.Invoke();
                }
                return;
            }

            if (command.type == NetCommandMessage.TypeSyncStart && !IsHost)
            {
                RefreshLobbyMembers();
                StatusMessage = "Partida online com " + OpponentName + " (Você = Jogador 2).";
                RaiseSessionChanged();
                if (OnMatchStartRequestedByHost != null)
                {
                    OnMatchStartRequestedByHost.Invoke();
                }
                return;
            }

            if (OnRemoteCommandReceived != null)
            {
                OnRemoteCommandReceived.Invoke(command);
            }
        }

        private void HandleGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            Debug.Log("[SteamMatchSession] Convite Steam aceito para sala: " + request.m_steamIDLobby.m_SteamID);
            JoinLobbyInternal(request.m_steamIDLobby);
        }

        private void RefreshLobbyMembers()
        {
            if (!IsInLobby || _currentLobbyId == CSteamID.Nil)
            {
                HasOpponent = false;
                OpponentName = string.Empty;
                return;
            }

            CSteamID localId = SteamUser.GetSteamID();
            int count = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyId);
            HasOpponent = count >= 2;
            OpponentName = string.Empty;

            for (int i = 0; i < count; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyId, i);
                if (member != localId)
                {
                    OpponentName = SteamFriends.GetFriendPersonaName(member);
                    if (string.IsNullOrWhiteSpace(OpponentName))
                    {
                        OpponentName = "Oponente";
                    }
                    break;
                }
            }
        }

        private void ShutdownSteam()
        {
            if (IsInLobby && _currentLobbyId != CSteamID.Nil && IsSteamAvailable)
            {
                SteamMatchmaking.LeaveLobby(_currentLobbyId);
                _currentLobbyId = CSteamID.Nil;
            }

            _lobbyCreatedCallResult?.Dispose();
            _lobbyCreatedCallResult = null;
            _lobbyEnteredCallback?.Dispose();
            _lobbyEnteredCallback = null;
            _lobbyChatUpdateCallback?.Dispose();
            _lobbyChatUpdateCallback = null;
            _lobbyChatMsgCallback?.Dispose();
            _lobbyChatMsgCallback = null;
            _joinRequestedCallback?.Dispose();
            _joinRequestedCallback = null;

            if (_ownsSteamInit && IsSteamAvailable)
            {
                IsSteamAvailable = false;
                _ownsSteamInit = false;
                SteamAPI.Shutdown();
            }
        }
#else
        private void ShutdownSteam()
        {
            IsSteamAvailable = false;
            IsInLobby = false;
            HasOpponent = false;
        }
#endif

        private void RaiseSessionChanged()
        {
            if (OnSessionChanged != null)
            {
                OnSessionChanged.Invoke();
            }
        }

        private void EmitNotice(string text)
        {
            if (OnSessionNotice != null && !string.IsNullOrEmpty(text))
            {
                OnSessionNotice.Invoke(text);
            }
        }
    }
}
