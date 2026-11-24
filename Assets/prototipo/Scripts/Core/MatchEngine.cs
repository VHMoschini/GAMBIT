using System;
using System.Collections.Generic;

namespace Prototipo.Core
{
    /// <summary>
    /// Motor principal da partida do protótipo: Setup de cápsulas, AP por turno,
    /// desbloqueio, movimentação, combate e condição de vitória.
    /// </summary>
    public sealed class MatchEngine
    {
        private static readonly IReadOnlyList<BoardCoord> EmptyCoords = Array.Empty<BoardCoord>();

        private readonly PlayerState player1;
        private readonly PlayerState player2;
        private readonly List<PieceInstance> pieces = new List<PieceInstance>();
        private int nextPieceId = 1;

        public MatchConfigData Config { get; }
        public BoardGrid Board { get; }
        public GamePhase Phase { get; private set; }
        public PlayerTeam ActiveTeam { get; private set; }
        public int TurnNumber { get; private set; }
        public PlayerTeam? Winner { get; private set; }
        public IReadOnlyList<PieceInstance> AllPieces => pieces;

        public event Action OnStateChanged;
        public event Action<PieceInstance> OnCapsulePlaced;
        public event Action<PieceInstance> OnCapsuleRemoved;
        public event Action<CapsuleUnlockedData> OnCapsuleUnlocked;
        public event Action<PieceMovedData> OnPieceMoved;
        public event Action<PieceAttackedData> OnPieceAttacked;
        public event Action<PlayerTeam, int> OnTurnChanged;
        public event Action<GamePhase> OnPhaseChanged;
        public event Action<PlayerTeam> OnGameOver;
        public event Action<string> OnLogMessage;

        public MatchEngine(MatchConfigData config = null, BoardGrid board = null)
        {
            Config = config ?? MatchConfigData.CreateDefault();
            Board = board ?? BoardGrid.CreateDefaultArena(Config.BoardWidth, Config.BoardHeight);
            player1 = new PlayerState(PlayerTeam.Player1, Config.ActionPointsPerTurn);
            player2 = new PlayerState(PlayerTeam.Player2, Config.ActionPointsPerTurn);

            Phase = GamePhase.Setup;
            ActiveTeam = PlayerTeam.Player1;
            TurnNumber = 0;
            Winner = null;
        }

        public PlayerState GetPlayer(PlayerTeam team)
        {
            return team == PlayerTeam.Player1 ? player1 : player2;
        }

        public PieceInstance GetPiece(int id)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Id == id)
                {
                    return pieces[i];
                }
            }

            return null;
        }

        public PieceInstance GetPieceAt(BoardCoord coord)
        {
            BoardTile tile = Board.GetTile(coord);
            return tile != null ? tile.Occupant : null;
        }

        public int GetPieceCount(PlayerTeam team, bool aliveOnly = true)
        {
            int count = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceInstance piece = pieces[i];
                if (piece.Owner == team && (!aliveOnly || piece.IsAlive))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetRemainingSetupCapsules(PlayerTeam team)
        {
            return Math.Max(0, Config.CapsulesPerPlayer - GetPieceCount(team, aliveOnly: false));
        }

        /// <summary>
        /// Permite confirmar o posicionamento assim que o jogador tiver ao menos 1 cápsula posicionada
        /// (aceita de 1 até <see cref="MatchConfigData.CapsulesPerPlayer"/> para facilitar testes rápidos).
        /// </summary>
        public bool CanConfirmSetup(PlayerTeam team)
        {
            return Phase == GamePhase.Setup
                && ActiveTeam == team
                && GetPieceCount(team, aliveOnly: false) >= 1;
        }

        public IReadOnlyList<BoardCoord> GetValidMoves(int pieceId, bool ignoreApCheck = false)
        {
            if (Phase != GamePhase.Playing)
            {
                return EmptyCoords;
            }

            PieceInstance piece = GetPiece(pieceId);
            if (piece == null || !piece.IsAlive || !piece.IsUnlocked || piece.HasActedThisTurn)
            {
                return EmptyCoords;
            }

            if (!ignoreApCheck)
            {
                if (piece.Owner != ActiveTeam)
                {
                    return EmptyCoords;
                }

                if (GetPlayer(piece.Owner).CurrentAP < piece.Spec.MoveCost)
                {
                    return EmptyCoords;
                }
            }

            return MovementCalculator.GetValidMoveDestinations(piece, Board);
        }

        public IReadOnlyList<BoardCoord> GetValidAttacks(int pieceId, bool ignoreApCheck = false)
        {
            if (Phase != GamePhase.Playing)
            {
                return EmptyCoords;
            }

            PieceInstance piece = GetPiece(pieceId);
            if (piece == null || !piece.IsAlive || !piece.IsUnlocked || piece.HasActedThisTurn)
            {
                return EmptyCoords;
            }

            if (!ignoreApCheck)
            {
                if (piece.Owner != ActiveTeam)
                {
                    return EmptyCoords;
                }

                if (GetPlayer(piece.Owner).CurrentAP < piece.Spec.AttackCost)
                {
                    return EmptyCoords;
                }
            }

            return MovementCalculator.GetValidAttackTargets(piece, Board);
        }

        public ActionResult PlaceSetupCapsule(PlayerTeam team, string specId, BoardCoord coord)
        {
            if (Phase != GamePhase.Setup)
            {
                return ActionResult.Fail("Posicionamento permitido apenas na fase Setup.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"Aguarde a vez do {ActiveTeam.ToDisplayName()}.");
            }

            if (GetRemainingSetupCapsules(team) <= 0)
            {
                return ActionResult.Fail("Limite de cápsulas atingido.");
            }

            PieceSpec spec = Config.FindSpec(specId);
            if (spec == null)
            {
                return ActionResult.Fail($"Tipo de cápsula desconhecido: '{specId}'.");
            }

            BoardTile tile = Board.GetTile(coord);
            if (tile == null)
            {
                return ActionResult.Fail("Casa fora do tabuleiro.");
            }

            if (!Board.IsSpawnTileFor(coord, team))
            {
                return ActionResult.Fail($"Casa {coord} não pertence à zona de spawn do {team.ToDisplayName()}.");
            }

            if (!tile.IsWalkable)
            {
                return ActionResult.Fail("Casa bloqueada ou buraco.");
            }

            if (tile.IsOccupied)
            {
                return ActionResult.Fail("Casa já ocupada por outra cápsula.");
            }

            var piece = new PieceInstance(nextPieceId++, spec, team, coord);
            tile.Occupant = piece;
            pieces.Add(piece);

            OnCapsulePlaced?.Invoke(piece);
            EmitLog($"{team.ToDisplayName()} posicionou cápsula {spec.DisplayName} em {coord}.");
            OnStateChanged?.Invoke();

            return ActionResult.Ok($"Cápsula {spec.DisplayName} posicionada.");
        }

        public ActionResult RemoveSetupCapsule(PlayerTeam team, BoardCoord coord)
        {
            if (Phase != GamePhase.Setup)
            {
                return ActionResult.Fail("Remoção permitida apenas na fase Setup.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"Aguarde a vez do {ActiveTeam.ToDisplayName()}.");
            }

            BoardTile tile = Board.GetTile(coord);
            if (tile == null || !tile.IsOccupied)
            {
                return ActionResult.Fail("Nenhuma cápsula nesta casa.");
            }

            PieceInstance piece = tile.Occupant;
            if (piece.Owner != team)
            {
                return ActionResult.Fail("Esta cápsula pertence ao oponente.");
            }

            tile.Occupant = null;
            pieces.Remove(piece);

            OnCapsuleRemoved?.Invoke(piece);
            EmitLog($"{team.ToDisplayName()} removeu cápsula de {coord}.");
            OnStateChanged?.Invoke();

            return ActionResult.Ok("Cápsula removida.");
        }

        public ActionResult AutoFillSetupForTeam(PlayerTeam team)
        {
            if (Phase != GamePhase.Setup)
            {
                return ActionResult.Fail("Auto-preenchimento permitido apenas na fase Setup.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"Aguarde a vez do {ActiveTeam.ToDisplayName()}.");
            }

            int remaining = GetRemainingSetupCapsules(team);
            if (remaining <= 0)
            {
                return ActionResult.Fail("Todas as cápsulas já estão posicionadas.");
            }

            IReadOnlyList<BoardCoord> spawnCoords = Board.GetSpawnCoords(team);
            int placed = 0;
            int catalogCount = Config.Catalog.Count;
            int specOffset = GetPieceCount(team, aliveOnly: false);

            for (int i = 0; i < spawnCoords.Count && placed < remaining; i++)
            {
                BoardCoord coord = spawnCoords[i];
                BoardTile tile = Board.GetTile(coord);
                if (tile == null || !tile.IsWalkable || tile.IsOccupied)
                {
                    continue;
                }

                PieceSpec spec = Config.Catalog[(specOffset + placed) % catalogCount];
                var piece = new PieceInstance(nextPieceId++, spec, team, coord);
                tile.Occupant = piece;
                pieces.Add(piece);
                placed++;

                OnCapsulePlaced?.Invoke(piece);
            }

            if (placed == 0)
            {
                return ActionResult.Fail("Não há casas livres na zona de spawn.");
            }

            EmitLog($"{team.ToDisplayName()} preencheu automaticamente {placed} cápsula(s).");
            OnStateChanged?.Invoke();

            return ActionResult.Ok($"{placed} cápsula(s) posicionada(s).");
        }

        public ActionResult ConfirmSetup(PlayerTeam team)
        {
            if (Phase != GamePhase.Setup)
            {
                return ActionResult.Fail("A partida já saiu da fase Setup.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"Aguarde a vez do {ActiveTeam.ToDisplayName()}.");
            }

            if (GetPieceCount(team, aliveOnly: false) < 1)
            {
                return ActionResult.Fail("Posicione ao menos 1 cápsula antes de confirmar.");
            }

            if (team == PlayerTeam.Player1)
            {
                ActiveTeam = PlayerTeam.Player2;
                EmitLog("Jogador 1 confirmou o posicionamento. Vez do Jogador 2 posicionar.");
                OnTurnChanged?.Invoke(ActiveTeam, TurnNumber);
                OnStateChanged?.Invoke();
                return ActionResult.Ok("Posicionamento confirmado. Vez do Jogador 2.");
            }

            Phase = GamePhase.Playing;
            TurnNumber = 1;
            ActiveTeam = PlayerTeam.Player1;
            player1.CurrentAP = player1.MaxAP;
            player2.CurrentAP = player2.MaxAP;
            ResetAllTurnActions();

            EmitLog("Batalha iniciada! Turno 1 — Jogador 1.");
            OnPhaseChanged?.Invoke(Phase);
            OnTurnChanged?.Invoke(ActiveTeam, TurnNumber);
            OnStateChanged?.Invoke();

            return ActionResult.Ok("Batalha iniciada!");
        }

        public ActionResult UnlockCapsule(PlayerTeam team, int pieceId)
        {
            if (Phase != GamePhase.Playing)
            {
                return ActionResult.Fail("Desbloqueio disponível apenas durante a batalha.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"É o turno do {ActiveTeam.ToDisplayName()}.");
            }

            PieceInstance piece = GetPiece(pieceId);
            if (piece == null || !piece.IsAlive)
            {
                return ActionResult.Fail("Peça não encontrada.");
            }

            if (piece.Owner != team)
            {
                return ActionResult.Fail("Você só pode desbloquear suas próprias cápsulas.");
            }

            if (piece.IsUnlocked)
            {
                return ActionResult.Fail("Esta cápsula já está desbloqueada.");
            }

            if (piece.HasActedThisTurn)
            {
                return ActionResult.Fail($"{piece.Spec.DisplayName} já agiu neste turno.");
            }

            PlayerState player = GetPlayer(team);
            int cost = piece.Spec.UnlockCost;
            if (player.CurrentAP < cost)
            {
                return ActionResult.Fail($"AP insuficiente! Necessário {cost} AP.");
            }

            player.CurrentAP -= cost;
            piece.Unlock();
            piece.MarkActedThisTurn();

            var data = new CapsuleUnlockedData(piece.Id, piece.Owner, piece.Position, cost);
            OnCapsuleUnlocked?.Invoke(data);
            EmitLog($"{team.ToDisplayName()} desbloqueou {piece.Spec.DisplayName} em {piece.Position} (-{cost} AP).");
            OnStateChanged?.Invoke();

            return ActionResult.Ok($"{piece.Spec.DisplayName} desbloqueada!");
        }

        public ActionResult MovePiece(PlayerTeam team, int pieceId, BoardCoord destination)
        {
            if (Phase != GamePhase.Playing)
            {
                return ActionResult.Fail("Movimento disponível apenas durante a batalha.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"É o turno do {ActiveTeam.ToDisplayName()}.");
            }

            PieceInstance piece = GetPiece(pieceId);
            if (piece == null || !piece.IsAlive)
            {
                return ActionResult.Fail("Peça não encontrada.");
            }

            if (piece.Owner != team)
            {
                return ActionResult.Fail("Você só pode mover suas próprias peças.");
            }

            if (!piece.IsUnlocked)
            {
                return ActionResult.Fail("Desbloqueie a cápsula antes de movê-la!");
            }

            if (piece.HasActedThisTurn)
            {
                return ActionResult.Fail($"{piece.Spec.DisplayName} já agiu neste turno.");
            }

            PlayerState player = GetPlayer(team);
            int cost = piece.Spec.MoveCost;
            if (player.CurrentAP < cost)
            {
                return ActionResult.Fail($"AP insuficiente! Movimento custa {cost} AP.");
            }

            if (!MovementCalculator.IsValidMoveDestination(piece, Board, destination))
            {
                return ActionResult.Fail($"Destino {destination} inválido para {piece.Spec.DisplayName}.");
            }

            BoardCoord from = piece.Position;
            BoardTile fromTile = Board.GetTile(from);
            BoardTile toTile = Board.GetTile(destination);

            player.CurrentAP -= cost;
            if (fromTile != null && fromTile.Occupant == piece)
            {
                fromTile.Occupant = null;
            }

            toTile.Occupant = piece;
            piece.MoveTo(destination);
            piece.MarkActedThisTurn();

            var data = new PieceMovedData(piece.Id, from, destination);
            OnPieceMoved?.Invoke(data);
            EmitLog($"{piece.Spec.DisplayName} ({team.ToDisplayName()}) moveu de {from} para {destination} (-{cost} AP).");
            OnStateChanged?.Invoke();

            return ActionResult.Ok("Movimento realizado.");
        }

        public ActionResult AttackTarget(PlayerTeam team, int attackerId, BoardCoord targetCoord)
        {
            if (Phase != GamePhase.Playing)
            {
                return ActionResult.Fail("Ataque disponível apenas durante a batalha.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"É o turno do {ActiveTeam.ToDisplayName()}.");
            }

            PieceInstance attacker = GetPiece(attackerId);
            if (attacker == null || !attacker.IsAlive)
            {
                return ActionResult.Fail("Atacante não encontrado.");
            }

            if (attacker.Owner != team)
            {
                return ActionResult.Fail("Você só pode atacar com suas próprias peças.");
            }

            if (!attacker.IsUnlocked)
            {
                return ActionResult.Fail("Desbloqueie a cápsula antes de atacar!");
            }

            if (attacker.HasActedThisTurn)
            {
                return ActionResult.Fail($"{attacker.Spec.DisplayName} já agiu neste turno.");
            }

            PlayerState player = GetPlayer(team);
            int cost = attacker.Spec.AttackCost;
            if (player.CurrentAP < cost)
            {
                return ActionResult.Fail($"AP insuficiente! Ataque custa {cost} AP.");
            }

            BoardTile targetTile = Board.GetTile(targetCoord);
            if (targetTile == null || !targetTile.IsOccupied || !targetTile.Occupant.IsAlive)
            {
                return ActionResult.Fail("Nenhum alvo válido nessa casa.");
            }

            PieceInstance target = targetTile.Occupant;
            if (target.Owner == team)
            {
                return ActionResult.Fail("Não é possível atacar uma peça aliada.");
            }

            int distance = attacker.Position.ChebyshevDistanceTo(targetCoord);
            if (distance > attacker.Spec.AttackRange)
            {
                return ActionResult.Fail($"Alvo fora do alcance ({attacker.Spec.AttackRange}).");
            }

            player.CurrentAP -= cost;
            attacker.MarkActedThisTurn();

            int hpBefore = target.CurrentHP;
            int effectiveDefense = target.IsUnlocked ? target.Spec.Defense : 0;
            int damage = Math.Max(1, attacker.Spec.Attack - effectiveDefense);
            target.ApplyDamage(damage);
            bool died = !target.IsAlive;

            if (died)
            {
                targetTile.Occupant = null;
            }

            var data = new PieceAttackedData(
                attacker.Id,
                target.Id,
                attacker.Position,
                targetCoord,
                damage,
                hpBefore,
                target.CurrentHP,
                died);

            OnPieceAttacked?.Invoke(data);
            EmitLog(died
                ? $"{attacker.Spec.DisplayName} derrotou {target.Spec.DisplayName} ({damage} de dano, -{cost} AP)!"
                : $"{attacker.Spec.DisplayName} atingiu {target.Spec.DisplayName} causando {damage} de dano (HP {target.CurrentHP}/{target.Spec.MaxHP}, -{cost} AP).");

            PlayerTeam opponent = team.Opponent();
            if (GetPieceCount(opponent, aliveOnly: true) == 0)
            {
                Phase = GamePhase.GameOver;
                Winner = team;
                EmitLog($"FIM DE JOGO! {team.ToDisplayName()} venceu a partida!");
                OnGameOver?.Invoke(team);
                OnPhaseChanged?.Invoke(Phase);
            }

            OnStateChanged?.Invoke();
            return ActionResult.Ok(died ? "Alvo derrotado!" : $"Dano causado: {damage}.");
        }

        public ActionResult EndTurn(PlayerTeam team)
        {
            if (Phase != GamePhase.Playing)
            {
                return ActionResult.Fail("Só é possível encerrar o turno durante a batalha.");
            }

            if (ActiveTeam != team)
            {
                return ActionResult.Fail($"É o turno do {ActiveTeam.ToDisplayName()}.");
            }

            ActiveTeam = team.Opponent();
            TurnNumber++;
            ResetAllTurnActions();

            PlayerState nextPlayer = GetPlayer(ActiveTeam);
            nextPlayer.CurrentAP = nextPlayer.MaxAP;

            EmitLog($"Turno {TurnNumber} — Vez do {ActiveTeam.ToDisplayName()} ({nextPlayer.CurrentAP} AP).");
            OnTurnChanged?.Invoke(ActiveTeam, TurnNumber);
            OnStateChanged?.Invoke();

            return ActionResult.Ok($"Turno {TurnNumber}: {ActiveTeam.ToDisplayName()}.");
        }

        private void ResetAllTurnActions()
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].ResetTurnAction();
            }
        }

        private void EmitLog(string message)
        {
            OnLogMessage?.Invoke(message);
        }
    }
}
