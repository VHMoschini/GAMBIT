using System.Collections.Generic;
using NUnit.Framework;
using Prototipo.Core;

namespace Prototipo.Tests
{
    public sealed class MatchEngineTests
    {
        private static MatchEngine CreatePlayingEngine(
            out PieceInstance p1Piece,
            out PieceInstance p2Piece,
            string p1SpecId = "red",
            BoardCoord? p1Coord = null,
            string p2SpecId = "red",
            BoardCoord? p2Coord = null,
            int actionPointsPerTurn = 6,
            BoardGrid customBoard = null)
        {
            var config = new MatchConfigData(
                boardWidth: 7,
                boardHeight: 7,
                capsulesPerPlayer: 4,
                actionPointsPerTurn: actionPointsPerTurn,
                catalog: DefaultPieceCatalog.CreateDefaultSpecs());

            var engine = new MatchEngine(config, customBoard);
            BoardCoord c1 = p1Coord ?? new BoardCoord(3, 1);
            BoardCoord c2 = p2Coord ?? new BoardCoord(3, 5);

            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, p1SpecId, c1).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, p2SpecId, c2).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player2).Success);

            p1Piece = engine.GetPieceAt(c1);
            p2Piece = engine.GetPieceAt(c2);
            return engine;
        }

        private static void AdvanceFullRound(MatchEngine engine)
        {
            PlayerTeam startTeam = engine.ActiveTeam;
            Assert.IsTrue(engine.EndTurn(startTeam).Success);
            Assert.IsTrue(engine.EndTurn(startTeam.Opponent()).Success);
            Assert.AreEqual(startTeam, engine.ActiveTeam);
        }

        [Test]
        public void Setup_PlaceRemoveAutoFillAndConfirm_TransitionsCorrectly()
        {
            var engine = new MatchEngine();
            Assert.AreEqual(GamePhase.Setup, engine.Phase);
            Assert.AreEqual(PlayerTeam.Player1, engine.ActiveTeam);
            Assert.IsFalse(engine.CanConfirmSetup(PlayerTeam.Player1));

            // Não permite confirmar sem nenhuma cápsula
            Assert.IsFalse(engine.ConfirmSetup(PlayerTeam.Player1).Success);

            // Não permite posicionar fora da spawn zone de P1
            Assert.IsFalse(engine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(3, 3)).Success);
            Assert.IsFalse(engine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(3, 5)).Success);

            // Não permite P2 jogar antes da sua vez no Setup
            Assert.IsFalse(engine.PlaceSetupCapsule(PlayerTeam.Player2, "red", new BoardCoord(3, 5)).Success);

            // Posicionamento válido e remoção
            var coordA = new BoardCoord(0, 0);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, "red", coordA).Success);
            Assert.AreEqual(3, engine.GetRemainingSetupCapsules(PlayerTeam.Player1));
            Assert.IsTrue(engine.CanConfirmSetup(PlayerTeam.Player1));

            // Casa já ocupada falha
            Assert.IsFalse(engine.PlaceSetupCapsule(PlayerTeam.Player1, "blue", coordA).Success);

            // Remoção libera a casa
            Assert.IsTrue(engine.RemoveSetupCapsule(PlayerTeam.Player1, coordA).Success);
            Assert.AreEqual(4, engine.GetRemainingSetupCapsules(PlayerTeam.Player1));
            Assert.IsNull(engine.GetPieceAt(coordA));

            // AutoFill preenche todas as 4 cápsulas
            Assert.IsTrue(engine.AutoFillSetupForTeam(PlayerTeam.Player1).Success);
            Assert.AreEqual(4, engine.GetPieceCount(PlayerTeam.Player1));
            Assert.AreEqual(0, engine.GetRemainingSetupCapsules(PlayerTeam.Player1));

            // Limite de cápsulas impede 5ª
            Assert.IsFalse(engine.PlaceSetupCapsule(PlayerTeam.Player1, "yellow", new BoardCoord(6, 1)).Success);

            // P1 confirma -> vez do P2
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.AreEqual(GamePhase.Setup, engine.Phase);
            Assert.AreEqual(PlayerTeam.Player2, engine.ActiveTeam);

            // P2 posiciona 2 cápsulas e confirma -> Playing Turn 1 P1
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "blue", new BoardCoord(2, 5)).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "yellow", new BoardCoord(4, 6)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player2).Success);

            Assert.AreEqual(GamePhase.Playing, engine.Phase);
            Assert.AreEqual(PlayerTeam.Player1, engine.ActiveTeam);
            Assert.AreEqual(1, engine.TurnNumber);
            Assert.AreEqual(6, engine.GetPlayer(PlayerTeam.Player1).CurrentAP);
            Assert.AreEqual(6, engine.GetPlayer(PlayerTeam.Player2).CurrentAP);
        }

        [Test]
        public void UnlockCapsule_SpendsAP_AndBlocksWithoutEnoughAP()
        {
            MatchEngine engine = CreatePlayingEngine(
                out PieceInstance p1Red,
                out _,
                p1SpecId: "red",
                p1Coord: new BoardCoord(3, 1),
                actionPointsPerTurn: 2);

            Assert.IsFalse(p1Red.IsUnlocked);
            // Red unlockCost é 3, mas jogador só tem 2 AP
            ActionResult failResult = engine.UnlockCapsule(PlayerTeam.Player1, p1Red.Id);
            Assert.IsFalse(failResult.Success);
            Assert.IsFalse(p1Red.IsUnlocked);
            Assert.AreEqual(2, engine.GetPlayer(PlayerTeam.Player1).CurrentAP);

            // Com 6 AP funciona e debita 3 AP
            MatchEngine richEngine = CreatePlayingEngine(out PieceInstance richRed, out _, p1SpecId: "red", actionPointsPerTurn: 6);
            ActionResult okResult = richEngine.UnlockCapsule(PlayerTeam.Player1, richRed.Id);
            Assert.IsTrue(okResult.Success);
            Assert.IsTrue(richRed.IsUnlocked);
            Assert.AreEqual(3, richEngine.GetPlayer(PlayerTeam.Player1).CurrentAP);

            // Não pode desbloquear novamente
            Assert.IsFalse(richEngine.UnlockCapsule(PlayerTeam.Player1, richRed.Id).Success);
        }

        [Test]
        public void ClosedCapsule_CannotMoveOrAttack()
        {
            MatchEngine engine = CreatePlayingEngine(
                out PieceInstance p1Red,
                out PieceInstance p2Red,
                p1SpecId: "red",
                p1Coord: new BoardCoord(3, 1),
                p2SpecId: "red",
                p2Coord: new BoardCoord(3, 5));

            Assert.AreEqual(0, engine.GetValidMoves(p1Red.Id).Count);
            Assert.AreEqual(0, engine.GetValidAttacks(p1Red.Id).Count);
            Assert.IsFalse(engine.MovePiece(PlayerTeam.Player1, p1Red.Id, new BoardCoord(3, 2)).Success);

            // Mesmo com inimigo adjacente, cápsula fechada não ataca
            var board = new BoardGrid(7, 7, coord =>
            {
                if (coord.Z <= 1) return TileKind.SpawnP1;
                if (coord.Z >= 5) return TileKind.SpawnP2;
                return TileKind.Normal;
            });
            var closeEngine = new MatchEngine(MatchConfigData.CreateDefault(), board);
            closeEngine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(3, 1));
            closeEngine.ConfirmSetup(PlayerTeam.Player1);
            closeEngine.PlaceSetupCapsule(PlayerTeam.Player2, "red", new BoardCoord(3, 5));
            closeEngine.ConfirmSetup(PlayerTeam.Player2);

            PieceInstance closedP1 = closeEngine.GetPieceAt(new BoardCoord(3, 1));
            Assert.IsFalse(closeEngine.AttackTarget(PlayerTeam.Player1, closedP1.Id, new BoardCoord(3, 5)).Success);
        }

        [Test]
        public void RedPiece_MovesOneStepOrthogonalOnly()
        {
            MatchEngine engine = CreatePlayingEngine(
                out PieceInstance p1Red,
                out _,
                p1SpecId: "red",
                p1Coord: new BoardCoord(3, 1));

            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player1, p1Red.Id).Success);
            Assert.IsTrue(p1Red.HasActedThisTurn);
            Assert.AreEqual(0, engine.GetValidMoves(p1Red.Id).Count);

            // No próximo turno do P1, a peça volta a poder agir
            AdvanceFullRound(engine);
            Assert.IsFalse(p1Red.HasActedThisTurn);

            IReadOnlyList<BoardCoord> moves = engine.GetValidMoves(p1Red.Id);
            Assert.AreEqual(4, moves.Count);
            CollectionAssert.Contains(moves, new BoardCoord(3, 2));
            CollectionAssert.Contains(moves, new BoardCoord(3, 0));
            CollectionAssert.Contains(moves, new BoardCoord(4, 1));
            CollectionAssert.Contains(moves, new BoardCoord(2, 1));

            // Diagonal e 2 casas são inválidas
            Assert.IsFalse(engine.MovePiece(PlayerTeam.Player1, p1Red.Id, new BoardCoord(4, 2)).Success);
            Assert.IsFalse(engine.MovePiece(PlayerTeam.Player1, p1Red.Id, new BoardCoord(3, 3)).Success);

            // Movimento válido atualiza posição, ocupação, AP e marca que já agiu
            Assert.IsTrue(engine.MovePiece(PlayerTeam.Player1, p1Red.Id, new BoardCoord(3, 2)).Success);
            Assert.AreEqual(new BoardCoord(3, 2), p1Red.Position);
            Assert.IsNull(engine.GetPieceAt(new BoardCoord(3, 1)));
            Assert.AreSame(p1Red, engine.GetPieceAt(new BoardCoord(3, 2)));
            Assert.AreEqual(4, engine.GetPlayer(PlayerTeam.Player1).CurrentAP);
            Assert.IsTrue(p1Red.HasActedThisTurn);
        }

        [Test]
        public void BluePiece_SlidesUpToTwoOrthogonal_AndIsBlockedByStoneHoleOrOccupant()
        {
            // Arena padrão tem Blocked em (2,3) e (4,3), e Hole em (1,3) e (5,3)
            var customEngine = new MatchEngine();
            Assert.IsTrue(customEngine.PlaceSetupCapsule(PlayerTeam.Player1, "blue", new BoardCoord(2, 1)).Success);
            Assert.IsTrue(customEngine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(3, 1)).Success);
            Assert.IsTrue(customEngine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.IsTrue(customEngine.PlaceSetupCapsule(PlayerTeam.Player2, "red", new BoardCoord(3, 5)).Success);
            Assert.IsTrue(customEngine.ConfirmSetup(PlayerTeam.Player2).Success);

            PieceInstance blue = customEngine.GetPieceAt(new BoardCoord(2, 1));
            Assert.IsTrue(customEngine.UnlockCapsule(PlayerTeam.Player1, blue.Id).Success);
            AdvanceFullRound(customEngine);

            IReadOnlyList<BoardCoord> moves = customEngine.GetValidMoves(blue.Id);

            // Norte: (2,2) livre, mas (2,3) é TileKind.Blocked -> (2,2) válido, (2,3) inválido
            CollectionAssert.Contains(moves, new BoardCoord(2, 2));
            CollectionAssert.DoesNotContain(moves, new BoardCoord(2, 3));

            // Leste: (3,1) está ocupado por aliado -> (3,1) e (4,1) bloqueados!
            CollectionAssert.DoesNotContain(moves, new BoardCoord(3, 1));
            CollectionAssert.DoesNotContain(moves, new BoardCoord(4, 1));

            // Oeste: (1,1) e (0,1) livres
            CollectionAssert.Contains(moves, new BoardCoord(1, 1));
            CollectionAssert.Contains(moves, new BoardCoord(0, 1));

            // Diagonais frontais para P1: NE (3,2) e NO (1,2)
            CollectionAssert.Contains(moves, new BoardCoord(3, 2));
            CollectionAssert.Contains(moves, new BoardCoord(1, 2));
            // Diagonais traseiras não existem para o Azul
            CollectionAssert.DoesNotContain(moves, new BoardCoord(3, 0));
            CollectionAssert.DoesNotContain(moves, new BoardCoord(1, 0));

            // Move para (1,1) e no próximo turno testa deslizamento norte atravessando Hole em (1,3):
            // (1,2) é livre, (1,3) é Hole -> não pode ir em (1,3)
            Assert.IsTrue(customEngine.MovePiece(PlayerTeam.Player1, blue.Id, new BoardCoord(1, 1)).Success);
            AdvanceFullRound(customEngine);
            IReadOnlyList<BoardCoord> movesFromHoleColumn = customEngine.GetValidMoves(blue.Id);
            CollectionAssert.Contains(movesFromHoleColumn, new BoardCoord(1, 2));
            CollectionAssert.DoesNotContain(movesFromHoleColumn, new BoardCoord(1, 3));
        }

        [Test]
        public void YellowPiece_MovesDiagonallyWithSlide_AndAttacksAtRange3()
        {
            var engine = new MatchEngine();
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, "yellow", new BoardCoord(3, 1)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "red", new BoardCoord(3, 5)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player2).Success);

            PieceInstance yellow = engine.GetPieceAt(new BoardCoord(3, 1));
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player1, yellow.Id).Success);
            AdvanceFullRound(engine);

            IReadOnlyList<BoardCoord> moves = engine.GetValidMoves(yellow.Id);
            // NE (4,2) livre, mas (5,3) é Hole -> (4,2) válido, (5,3) bloqueado!
            CollectionAssert.Contains(moves, new BoardCoord(4, 2));
            CollectionAssert.DoesNotContain(moves, new BoardCoord(5, 3));
            // NO (2,2) livre, (1,3) é Hole -> (2,2) válido, (1,3) bloqueado!
            CollectionAssert.Contains(moves, new BoardCoord(2, 2));
            CollectionAssert.DoesNotContain(moves, new BoardCoord(1, 3));
            // Recuo (3,0) válido
            CollectionAssert.Contains(moves, new BoardCoord(3, 0));

            // Move para (4,2): agora Chebyshev até (3,5) é max(|4-3|, |2-5|) = 3 (alcance máximo do Amarelo!)
            Assert.IsTrue(engine.MovePiece(PlayerTeam.Player1, yellow.Id, new BoardCoord(4, 2)).Success);
            // Já agiu neste turno -> só ataca no próximo turno
            Assert.AreEqual(0, engine.GetValidAttacks(yellow.Id).Count);
            AdvanceFullRound(engine);

            IReadOnlyList<BoardCoord> targets = engine.GetValidAttacks(yellow.Id);
            CollectionAssert.Contains(targets, new BoardCoord(3, 5));

            // Ataca a distância 3 contra cápsula fechada (DEF 0 -> 4 de dano)
            ActionResult attackRes = engine.AttackTarget(PlayerTeam.Player1, yellow.Id, new BoardCoord(3, 5));
            Assert.IsTrue(attackRes.Success);
            Assert.AreEqual(8, engine.GetPieceAt(new BoardCoord(3, 5)).CurrentHP); // 12 - 4 = 8
            Assert.AreEqual(4, engine.GetPlayer(PlayerTeam.Player1).CurrentAP);
        }

        [Test]
        public void Player2_MirrorsMovePatterns180Degrees()
        {
            var engine = new MatchEngine();
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(0, 0)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "blue", new BoardCoord(3, 5)).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "yellow", new BoardCoord(6, 6)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player2).Success);

            // Passa vez para o Player 2 desbloquear ambas as peças
            Assert.IsTrue(engine.EndTurn(PlayerTeam.Player1).Success);
            Assert.AreEqual(PlayerTeam.Player2, engine.ActiveTeam);

            PieceInstance p2Blue = engine.GetPieceAt(new BoardCoord(3, 5));
            PieceInstance p2Yellow = engine.GetPieceAt(new BoardCoord(6, 6));
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player2, p2Blue.Id).Success);
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player2, p2Yellow.Id).Success);

            // Próximo turno do P2 para avaliar movimentos após desbloqueio
            AdvanceFullRound(engine);

            IReadOnlyList<BoardCoord> blueMoves = engine.GetValidMoves(p2Blue.Id);

            // Para P2, frente é -Z: diagonais frontais do Azul são (-1,-1) -> (2,4) e (1,-1) -> (4,4)
            CollectionAssert.Contains(blueMoves, new BoardCoord(2, 4));
            CollectionAssert.Contains(blueMoves, new BoardCoord(4, 4));
            // Diagonais traseiras para P2 são (2,6) e (4,6) -> não permitidas
            CollectionAssert.DoesNotContain(blueMoves, new BoardCoord(2, 6));
            CollectionAssert.DoesNotContain(blueMoves, new BoardCoord(4, 6));

            // Amarelo do P2 em (6,6): diagonais frontais para (5,5) e (4,4)
            IReadOnlyList<BoardCoord> yellowMoves = engine.GetValidMoves(p2Yellow.Id);
            CollectionAssert.Contains(yellowMoves, new BoardCoord(5, 5));
            CollectionAssert.Contains(yellowMoves, new BoardCoord(4, 4));
        }

        [Test]
        public void Combat_AppliesDefenseWhenUnlocked_IgnoresDefenseWhenClosed_AndTriggersVictoryOnLastKill()
        {
            // Tabuleiro livre para colocar P1 e P2 próximos e testar combate/morte/vitória rapidamente
            var board = new BoardGrid(7, 7, coord =>
            {
                if (coord.Z <= 1) return TileKind.SpawnP1;
                if (coord.Z >= 2) return TileKind.SpawnP2;
                return TileKind.Normal;
            });

            var engine = new MatchEngine(new MatchConfigData(7, 7, 4, 10, DefaultPieceCatalog.CreateDefaultSpecs()), board);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(3, 1)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "yellow", new BoardCoord(3, 2)).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "red", new BoardCoord(4, 2)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player2).Success);

            PieceInstance p1Red = engine.GetPieceAt(new BoardCoord(3, 1));
            PieceInstance p2Yellow = engine.GetPieceAt(new BoardCoord(3, 2));
            PieceInstance p2Red = engine.GetPieceAt(new BoardCoord(4, 2));

            // Turno 1: P1 desbloqueia p1Red -> já agiu neste turno
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player1, p1Red.Id).Success);
            AdvanceFullRound(engine);

            // Ataca p2Red ainda fechado (DEF 0): ATK 5 - 0 = 5 de dano (12 -> 7)
            PieceAttackedData? lastAttack = null;
            engine.OnPieceAttacked += data => lastAttack = data;
            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Red.Id, new BoardCoord(4, 2)).Success);
            Assert.IsTrue(lastAttack.HasValue);
            Assert.AreEqual(5, lastAttack.Value.Damage);
            Assert.AreEqual(7, p2Red.CurrentHP);

            // Passa turno para P2 desbloquear p2Red (agora passa a ter DEF 2)
            Assert.IsTrue(engine.EndTurn(PlayerTeam.Player1).Success);
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player2, p2Red.Id).Success);
            Assert.IsTrue(engine.EndTurn(PlayerTeam.Player2).Success);

            // Volta para P1. Ataca p2Red desbloqueado: ATK 5 - DEF 2 = 3 (7 -> 4)
            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Red.Id, new BoardCoord(4, 2)).Success);
            Assert.AreEqual(4, p2Red.CurrentHP);

            // Segundo golpe no turno seguinte (4 -> 1) e terceiro golpe mata
            AdvanceFullRound(engine);
            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Red.Id, new BoardCoord(4, 2)).Success);
            Assert.AreEqual(1, p2Red.CurrentHP);
            AdvanceFullRound(engine);
            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Red.Id, new BoardCoord(4, 2)).Success);
            Assert.IsFalse(p2Red.IsAlive);
            Assert.IsNull(engine.GetPieceAt(new BoardCoord(4, 2))); // Casa liberada após morte

            // Agora mata o último inimigo (p2Yellow fechado, 6 HP): 5 de dano num turno e mais 5 no próximo
            AdvanceFullRound(engine);

            PlayerTeam? winnerFromEvent = null;
            GamePhase? phaseFromEvent = null;
            engine.OnGameOver += w => winnerFromEvent = w;
            engine.OnPhaseChanged += p => phaseFromEvent = p;

            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Red.Id, new BoardCoord(3, 2)).Success);
            Assert.AreEqual(1, p2Yellow.CurrentHP);
            Assert.AreEqual(GamePhase.Playing, engine.Phase);

            AdvanceFullRound(engine);
            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Red.Id, new BoardCoord(3, 2)).Success);
            Assert.IsFalse(p2Yellow.IsAlive);
            Assert.AreEqual(GamePhase.GameOver, engine.Phase);
            Assert.AreEqual(PlayerTeam.Player1, engine.Winner);
            Assert.AreEqual(PlayerTeam.Player1, winnerFromEvent);
            Assert.AreEqual(GamePhase.GameOver, phaseFromEvent);

            // Após GameOver, não permite mais ações
            Assert.IsFalse(engine.EndTurn(PlayerTeam.Player1).Success);
        }

        [Test]
        public void Piece_CanOnlyActOncePerTurn_UnlockMoveOrAttack_AndResetsOnNextTurn()
        {
            var board = new BoardGrid(7, 7, coord =>
            {
                if (coord.Z <= 1) return TileKind.SpawnP1;
                if (coord.Z >= 2) return TileKind.SpawnP2;
                return TileKind.Normal;
            });

            var engine = new MatchEngine(new MatchConfigData(7, 7, 4, 10, DefaultPieceCatalog.CreateDefaultSpecs()), board);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, "blue", new BoardCoord(3, 1)).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player1, "red", new BoardCoord(1, 1)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player1).Success);
            Assert.IsTrue(engine.PlaceSetupCapsule(PlayerTeam.Player2, "red", new BoardCoord(3, 2)).Success);
            Assert.IsTrue(engine.ConfirmSetup(PlayerTeam.Player2).Success);

            PieceInstance p1Blue = engine.GetPieceAt(new BoardCoord(3, 1));
            PieceInstance p1Red = engine.GetPieceAt(new BoardCoord(1, 1));

            // 1. Desencapsular consome a interação única da peça no turno (mas outra peça ainda pode agir!)
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player1, p1Blue.Id).Success);
            Assert.IsTrue(p1Blue.HasActedThisTurn);
            Assert.IsFalse(p1Red.HasActedThisTurn);
            Assert.AreEqual(0, engine.GetValidMoves(p1Blue.Id).Count);
            Assert.AreEqual(0, engine.GetValidAttacks(p1Blue.Id).Count);
            Assert.IsFalse(engine.MovePiece(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(2, 1)).Success);
            Assert.IsFalse(engine.AttackTarget(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(3, 2)).Success);

            // Outra peça do mesmo jogador ainda consegue desencapsular no mesmo turno
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player1, p1Red.Id).Success);
            Assert.IsTrue(p1Red.HasActedThisTurn);

            // 2. EndTurn reseta HasActedThisTurn
            AdvanceFullRound(engine);
            Assert.IsFalse(p1Blue.HasActedThisTurn);
            Assert.IsFalse(p1Red.HasActedThisTurn);

            // 3. Mover consome a interação única -> não pode mover de novo nem atacar
            Assert.IsTrue(engine.MovePiece(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(2, 1)).Success);
            Assert.IsTrue(p1Blue.HasActedThisTurn);
            Assert.AreEqual(0, engine.GetValidMoves(p1Blue.Id).Count);
            Assert.AreEqual(0, engine.GetValidAttacks(p1Blue.Id).Count);
            Assert.IsFalse(engine.MovePiece(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(1, 1)).Success);
            Assert.IsFalse(engine.AttackTarget(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(3, 2)).Success);

            // 4. Atacar consome a interação única -> não pode atacar de novo nem mover
            AdvanceFullRound(engine);
            Assert.IsTrue(engine.AttackTarget(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(3, 2)).Success);
            Assert.IsTrue(p1Blue.HasActedThisTurn);
            Assert.AreEqual(0, engine.GetValidMoves(p1Blue.Id).Count);
            Assert.AreEqual(0, engine.GetValidAttacks(p1Blue.Id).Count);
            Assert.IsFalse(engine.AttackTarget(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(3, 2)).Success);
            Assert.IsFalse(engine.MovePiece(PlayerTeam.Player1, p1Blue.Id, new BoardCoord(3, 1)).Success);
        }

        [Test]
        public void EndTurn_AlternatesTeam_IncrementsTurn_AndRefillsAP()
        {
            MatchEngine engine = CreatePlayingEngine(
                out PieceInstance p1Red,
                out _,
                p1SpecId: "red",
                actionPointsPerTurn: 6);

            Assert.AreEqual(1, engine.TurnNumber);
            Assert.AreEqual(PlayerTeam.Player1, engine.ActiveTeam);
            Assert.IsTrue(engine.UnlockCapsule(PlayerTeam.Player1, p1Red.Id).Success);
            Assert.AreEqual(3, engine.GetPlayer(PlayerTeam.Player1).CurrentAP);

            Assert.IsTrue(engine.EndTurn(PlayerTeam.Player1).Success);
            Assert.AreEqual(2, engine.TurnNumber);
            Assert.AreEqual(PlayerTeam.Player2, engine.ActiveTeam);
            Assert.AreEqual(6, engine.GetPlayer(PlayerTeam.Player2).CurrentAP);

            Assert.IsTrue(engine.EndTurn(PlayerTeam.Player2).Success);
            Assert.AreEqual(3, engine.TurnNumber);
            Assert.AreEqual(PlayerTeam.Player1, engine.ActiveTeam);
            Assert.AreEqual(6, engine.GetPlayer(PlayerTeam.Player1).CurrentAP);
        }
    }
}
