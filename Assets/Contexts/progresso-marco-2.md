# Progresso — Marco 2: Uma peça que anda (vertical slice)

> Sessão de 2026-06-23. Sistemas entregues (parciais, por design): **Piece System + Command Pipeline (mínimos)**.
> Companion: `arquitetura-core-v5.md` (seções 5 e 6) e `ordem-desenvolvimento.md` (Marco 2).

---

## Entregável do marco

> *"Clicar numa peça → ver o alcance destacado (`ReachableTilesComputedEvent`) → clicar num tile válido → a peça anda, com a animação tocando a partir do payload do evento."*

É o vertical slice que prova a arquitetura inteira de ponta a ponta: **input → comando → core → evento → visual**.

- ✅ **Pipeline mínimo:** `MoveCommand` + `CommandProcessor.Submit` + validação em duas camadas + log (`History`).
- ✅ **Piece mínimo:** `PieceDefinition`/`Piece` + `MovePattern` + `MoveActionDefinition`.
- ✅ **Movimento v6:** padrões fixos por peça, deslizar com caminho inteiro livre, frente relativa ao dono.
- ✅ **Fronteira core→visual por delta:** `ReachableTilesComputedEvent`/`Cleared`, `PieceMovedEvent` (fato completo com `from`), `CommandRejectedEvent` — todos pelo bus.
- ✅ **Testes de edit mode:** resolver de movimento, `MoveCommand` (validação + execução) e `CommandProcessor`.

---

## O que foi construído

### Core — Piece System (`Game.Core.Pieces`)

| Arquivo | Papel |
|---------|-------|
| `Pieces/PlayerId.cs` | Enum do lado/dono (`Player1`/`Player2`). A "frente" é relativa ao dono. |
| `Pieces/PieceId.cs` | Struct de identidade por valor (casa payload de evento ↔ view). Atribuição sequencial via processor vem no M3. |
| `Pieces/MovePattern.cs` | `[Serializable]` — trajetória canônica fixa (`Steps`, relativos à origem; destino = último). Construtor público p/ tooling/testes. |
| `Pieces/MoveResolver.cs` | **Política de movimento (POCO puro):** `Orient` (espelha por dono), `TryResolvePath` (deslizar = caminho inteiro livre), `GetPlayableDestinations`, `IsPlayableDestination`. Reusado pelo controller (destaque) e pelo `MoveCommand.Validate`. |
| `Pieces/Piece.cs` | Instância viva: `Definition`, `Id`, `Owner`, `Position` (mutável só via `MoveTo`). Sem HP/stats (M3). |
| `Pieces/PieceDefinition.cs` | `ScriptableObject` molde: `DisplayName`, `MovePatterns`, `Actions`. `CreateAssetMenu` em `GAMBIT/Piece Definition`. Factory `Create(...)` p/ tooling/testes. |
| `Pieces/Actions/ActionDefinition.cs` | SO abstrato: `DisplayName` + `abstract ICommand BuildCommand(Piece, GridCoord)`. Polimorfismo p/ ação nova = classe nova. |
| `Pieces/Actions/MoveActionDefinition.cs` | Constrói `MoveCommand` para a casa-destino. `CreateAssetMenu` em `GAMBIT/Actions/Move`. |

### Core — Command Pipeline (`Game.Core.Commands`)

| Arquivo | Papel |
|---------|-------|
| `Commands/Result.cs` | `Ok` / `Reject(motivo)` — devolvido ao chamador. |
| `Commands/ICommand.cs` | `Validate(ctx) → Result` (nada muta) + `Execute(ctx)` (muta via donos + publica eventos). |
| `Commands/GameContext.cs` | Fachada: `Grid` + `Events`. (`Players`/`CurrentPlayer` no M4; `GetPiece` no M3.) |
| `Commands/MoveCommand.cs` | Autossuficiente (`owner/piece/from/to`). Valida posse + destino jogável; executa via `Grid.ClearOccupant/SetOccupant` + `Piece.MoveTo`; publica `PieceMovedEvent`. |
| `Commands/CommandProcessor.cs` | `Submit`: **Camada 1** (`RunGlobalPreChecks`, gancho de turno/fase/stamina — vazio no M2) → **Camada 2** (`Validate`) → `Execute` → `History`. Recusa publica `CommandRejectedEvent`. |

### Core — Eventos (`Game.Core.Events`)

- `PieceMovedEvent { PieceId, from, to }` — **fato completo** (carrega `from`).
- `ReachableTilesComputedEvent { PlayerId, IReadOnlyList<GridCoord> }` / `ReachableTilesClearedEvent { PlayerId }`.
- `CommandRejectedEvent { command, reason }`.

### Core — Grid (incremento do M1)

- `Grid/Tile.cs`: `Occupant` (set interno) + `IsOccupied` derivado. Era a pendência declarada do M1.
- `Grid/GridSystem.cs`: `SetOccupant`/`ClearOccupant` (porta pública da mutação de ocupação).

### Editor (`Game.Core.Editor`)

- `Editor/PieceDemoBuilder.cs`: menu **`GAMBIT/Pieces/Create Demo Piece`** → cria `Move.asset` + `Runner.asset` (com vários `MovePattern`) sob `Assets/PieceDemo`.

### Visual (`Game.Visual`)

| Arquivo | Papel |
|---------|-------|
| `Grid/GridVisualizer.cs` | (incremento) build idempotente (`EnsureBuilt`); expõe `TileObjects`, `TryGetCoord`, `TopOf`. |
| `Grid/TileHighlighter.cs` | Escuta `ReachableTilesComputed/Cleared` e pinta/restaura os tiles. |
| `Pieces/PieceView.cs` | Anima o deslocamento a partir do **payload** de `PieceMovedEvent` (lê `from`/`to`, nunca o estado atual). |
| `Match/MatchController.cs` | Bootstrap do slice: monta bus + processor + contexto, posiciona a peça, traduz cliques (raycast) em seleção/movimento. Ancestral mínimo do `HumanController` (M4). |

### Testes de edit mode (`Game.Core.Tests`)

- `MoveResolverTests.cs` — destino = último passo; espelhamento do Player2 (180°); ocupante intermediário invalida o padrão (deslizar); buraco no caminho invalida; destino fora do grid não jogável.
- `MoveCommandTests.cs` — move válido executa (ocupação migra, `Position` atualiza, `PieceMovedEvent` com `from` correto); dono errado recusa; destino não jogável recusa; sem peça na origem recusa.
- `CommandProcessorTests.cs` — `Submit` válido loga + publica `PieceMovedEvent`; `Submit` inválido publica `CommandRejectedEvent` e não loga.

---

## Como demonstrar (passo a passo no Unity)

1. **`GAMBIT/Grid/Create Demo Grid`** → tabuleiro 7×7 irregular.
2. **`GAMBIT/Grid/Bake Scene To GridData`** → gera o `GridData` (escolha onde salvar).
3. **`GAMBIT/Pieces/Create Demo Piece`** → gera `Assets/PieceDemo/Runner.asset` (+ `Move.asset`).
4. Na cena, num objeto vazio, adicione **`MatchController`** e ligue:
   - `GridRuntime` (com o `GridData` baked) — adicione um `GridRuntime` apontando para o mesmo `GridData`.
   - `GridVisualizer` (mesmo `GridData`) e `TileHighlighter` (apontando para o `GridVisualizer`).
   - `Piece Definition` = `Runner`; `Spawn Coord` = uma casa de grama livre (ex: (3,1)); `Owner` = Player1.
   - `Click Camera` = a câmera (ou deixe vazio → usa `Camera.main`).
5. **Play** → clique na cápsula (peça) → as casas-destino acendem → clique numa casa acesa → a peça desliza até lá.
6. **Test Runner** (EditMode > Run All) → FSM + EventBus + Grid + **Move/Pipeline** passando.

> ⚠️ **Mesma `GridData` em `GridRuntime` e `GridVisualizer`** — o controller muta a ocupação no `GridRuntime.Grid` e o highlight/posições leem do `GridVisualizer`; as coordenadas precisam coincidir.

---

## Decisões e desvios desta sessão

- **`Occupant` no `Tile` (pendência do M1) entregue.** Mutável só via `GridSystem.SetOccupant/ClearOccupant`; `IsOccupied` derivado, nunca armazenado.
- **Validação em duas camadas com Camada 1 vazia (gancho deliberado).** Sem Turn System (M4), não há turno/fase/stamina a checar; `RunGlobalPreChecks` existe como ponto de extensão documentado, não como branch especulativo (é costura de design, ver `code-patterns.md` §2).
- **Sem registro `PieceId → Piece` no processor (adiado p/ M3).** O `MoveCommand` resolve a peça pelo `Occupant` da casa de origem; o `GameContext` carrega só `Grid` + `Events`. `GetPiece` nasce com o `SummonCommand`/morte no M3.
- **`PieceId` atribuído pelo bootstrap da cena**, não pelo processor — a atribuição sequencial via `SummonCommand` (âncora de replay) entra no M3.
- **`Piece` sem HP/stats e `PieceDefinition` sem Attack/Defense/MaxHP/IsVictoryCritical.** Entram no M3, quando ataque/dano/morte os exercitam (ordem-desenvolvimento).
- **Espelhamento do Player2 = rotação 180°** (`-x, -z`). Suficiente para dois lados; revisitar se surgir assimetria esquerda/direita relevante.
- **Fila de comandos derivados não construída** (FIFO chega com lava/dano no M3) — `Submit` executa e loga sem fila.
- **`GameContext` carrega o bus (`Events`).** O `Execute` publica os eventos do resultado; o bus é tratado como sistema dono do dispatch.
- **`MatchController` é o ancestral mínimo do `HumanController`** — sem wizard em pilha, sem gate de input (`PresentationFinishedEvent`, M5): um clique durante a animação vira comando que o pipeline valida normalmente (correção não depende do gate).

## Pendências / fora do escopo do M2

- Rodar a suíte no Test Runner do Unity (ação manual).
- Wizard de seleção em pilha, turno, stamina, dois controllers (M4).
- `AttackCommand`/`DamageCommand`, `CurrentHP`, morte, registro `PieceId → Piece` (M3).
- Gate de input durante a apresentação, `SummonCommand` de setup, golden-log de replay (M5).
- Bloqueio por diferença de altura (`MaxStepHeight`) na validação de movimento (M5).

---

## Próximo: Marco 3 — Duas peças brigam

`AttackCommand`, `DamageCommand` (derivado), `CurrentHP`, evento de morte, registro `PieceId → Piece` no processor; fila de derivados (move → dano → morte na ordem certa).
