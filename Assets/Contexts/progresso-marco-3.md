# Progresso — Marco 3: Duas peças brigam

> Sessão de 2026-07-07. Sistemas incrementados: **Command Pipeline + Piece System** (ataque/dano/morte).
> Companion: `arquitetura-core-v5.md` (seções 5 e 6) e `ordem-desenvolvimento.md` (Marco 3).

---

## Entregável do marco

> *"Uma peça ataca outra, dano resolve, peça morre e some — com a fila de derivados funcionando
> (move → dano → morte na ordem certa)."*

- ✅ **Ataque:** `AttackCommand` (posse + alvo inimigo + alcance por Chebyshev; LOS fora de escopo).
- ✅ **Dano derivado:** `AttackCommand.Execute` **enfileira** `DamageCommand` (não inline) — a cadeia
  resolve pela fila FIFO do processor, na ordem real (dano → morte), sem recursão.
- ✅ **Morte:** resolvida **dentro** do `DamageCommand` quando a vida zera — limpa ocupação, desregistra e
  publica `PieceDiedEvent` logo após o `DamagedEvent`.
- ✅ **Registro `PieceId → Piece`:** `PieceRegistry` no pipeline, exposto pelo `GameContext.GetPiece`.
- ✅ **Fronteira core→visual por delta:** `DamagedEvent`/`PieceDiedEvent` (fato completo) pelo bus; a peça
  visual some ao receber o `PieceDiedEvent`.
- ✅ **Testes de edit mode:** `PieceRegistry`, `AttackCommand` (validação + cadeia derivada via processor),
  `DamageCommand` (dano/morte + ordem dos eventos).

---

## O que foi construído

### Core — Piece System (`Game.Core.Pieces`)

| Arquivo | Mudança |
|---------|---------|
| `Pieces/Piece.cs` | +`CurrentHP` (inicia de `Definition.MaxHP`), `IsAlive` (derivado), `TakeDamage(amount)` (clampa em 0, não publica evento). |
| `Pieces/PieceDefinition.cs` | +`Attack`/`Defense`/`MaxHP` (+ params no factory `Create`). **`IsVictoryCritical` adiado p/ M4** (sem leitor em M3 — ver desvios). |
| `Pieces/PieceRegistry.cs` | **Novo.** Índice `PieceId → Piece`: `Register`/`Unregister`/`Get`. Porta única de resolução id → peça. |
| `Pieces/Actions/ActionDefinition.cs` | `BuildCommand` passa a receber `GameContext` (assinatura da seção 6, adiada no M2) — permite resolver o alvo pelo ocupante da casa. |
| `Pieces/Actions/MoveActionDefinition.cs` | Assinatura nova (ignora o `context`). |
| `Pieces/Actions/AttackActionDefinition.cs` | **Novo.** `Range` (Chebyshev); resolve o alvo pelo ocupante da casa-alvo → `AttackCommand`. `CreateAssetMenu` em `GAMBIT/Actions/Attack`. |

### Core — Command Pipeline (`Game.Core.Commands`)

| Arquivo | Mudança |
|---------|---------|
| `Commands/GameContext.cs` | +`Pieces` (registro) + `GetPiece(id)`; +**fila de derivados** (`EnqueueDerived` público; `TryDequeueDerived` interno, só o processor drena). |
| `Commands/AttackCommand.cs` | **Novo.** `{ owner, attacker, target, range }`. Valida posse/inimigo/alcance; `Execute` enfileira `DamageCommand` com `amount = max(1, Atk − Def)`. |
| `Commands/DamageCommand.cs` | **Novo.** `{ target, amount }` (derivado). Valida alvo vivo/registrado; `Execute` aplica dano → `DamagedEvent`; se letal → `ClearOccupant` + `Unregister` + `PieceDiedEvent`. |
| `Commands/CommandProcessor.cs` | `Submit` drena a **fila de derivados** após a jogada (FIFO iterativo, pula Camada 1, mantém `Validate`, loga no `History`). +`Register(piece)` (porta de entrada do registro). |

### Core — Eventos (`Game.Core.Events`)

- `DamagedEvent { PieceId Target, int Amount, int HpBefore, int HpAfter }` — fato completo.
- `PieceDiedEvent { PieceId Piece, PlayerId Owner, GridCoord At }` — fato completo (owner/at no payload).

### Visual (`Game.Visual`)

| Arquivo | Mudança |
|---------|---------|
| `Pieces/PieceView.cs` | +escuta `PieceDiedEvent`: some (`Destroy`) quando o id casa. Desinscreve dos dois handlers no `OnDestroy`. |
| `Match/MatchController.cs` | Constrói o `PieceRegistry` no contexto; **spawna 2 peças** (Player1 + inimigo Player2) e registra ambas; clique em inimigo no alcance → `AttackCommand`. Ids começam em **1** (0 reservado como "sem alvo"). Remove o `Collider` da cápsula (o raycast resolve sempre o tile embaixo). |

### Editor (`Game.Core.Editor`)

- `Editor/PieceDemoBuilder.cs`: agora cria `Attack.asset`, seta stats no **Runner** (Atk 3 / Def 0 / HP 5, com Move + Attack) e cria um **Target** (Atk 1 / Def 1 / HP 2 — morre num golpe do Runner).

### Testes de edit mode (`Game.Core.Tests`)

- `PieceRegistryTests.cs` — register/get/unregister; id duplicado e remoção desconhecida lançam.
- `DamageCommandTests.cs` — dano não-letal (fato com hpBefore/hpAfter, segue no grid/registro); letal (casa liberada, desregistrada, `Damaged` **antes** de `Died`, owner/at no payload); alvo não registrado recusa.
- `AttackCommandTests.cs` — validação (inimigo no alcance ok; aliado/dono errado/fora de alcance recusam); **cadeia derivada via processor** (dano→morte na ordem, alvo sai do grid/registro, `History` = `[Attack, Damage]`); ataque não-letal sobrevive.
- Atualizados p/ o novo `GameContext(grid, bus, registry)`: `MoveCommandTests`, `CommandProcessorTests`.

---

## Como demonstrar (passo a passo no Unity)

1. **`GAMBIT/Grid/Create Demo Grid`** → tabuleiro; **`GAMBIT/Grid/Bake Scene To GridData`** → gera o `GridData`.
2. **`GAMBIT/Pieces/Create Demo Piece`** → gera `Runner`, `Target`, `Move`, `Attack` em `Assets/PieceDemo`.
3. No `MatchController` da cena: `Piece Definition` = **Runner**, `Spawn Coord` = casa livre (ex: (3,1));
   `Enemy Definition` = **Target**, `Enemy Spawn Coord` = casa adjacente ao Runner (ex: (3,2)).
   Ligue `GridRuntime`/`GridVisualizer`/`TileHighlighter` como no M2 (mesma `GridData`).
4. **Play** → clique no Runner (casas de movimento acendem) → clique no **Target adjacente** → o Runner
   ataca, o dano resolve e o Target **some**.
5. **Test Runner** (EditMode > Run All) → FSM + EventBus + Grid + Move/Pipeline + **Attack/Damage/Registry** passando.

> ⚠️ Alcance de ataque é **Chebyshev** (range 1 = adjacente, inclui diagonal). Posicione o Target dentro do alcance.

---

## Decisões e desvios desta sessão

- **`IsVictoryCritical` adiado para o M4 (desvio do roadmap, por §2 de code-patterns).** O roadmap agrupava
  Atk/Def/HP/IsVictoryCritical no M3, mas em M3 **nenhum caso lê** `IsVictoryCritical` — o juiz de vitória
  é M4. Campo sem leitor é cortado até o caso chegar; entra junto do juiz no M4. Atk/Def/HP entraram porque
  ataque/dano os exercitam agora.
- **Morte resolvida dentro do `DamageCommand`, não como comando separado.** Casa com o exemplo da seção 5
  ("fila: DamageCommand → Execute → peça morre"): morte é consequência do dano, não uma unidade de jogada.
  Ordem de eventos garantida: `DamagedEvent` e então `PieceDiedEvent`.
- **Fórmula de dano = `max(1, Attack − Defense)` (placeholder).** Determinística (sem RNG), sempre chipa ao
  menos 1. Isolada no `AttackCommand.Execute` — confirmar contra o jogo base quando houver acesso.
- **`amount` computado no `AttackCommand`, carregado pelo `DamageCommand`.** Mantém o `DamageCommand`
  genérico (a lava/terreno do M5 reusa com um amount fixo, sem stats de atacante).
- **Fila de derivados exposta pelo `GameContext`, drenada só pelo `CommandProcessor`.** O `Execute` enfileira
  via `context.EnqueueDerived`; o processor é o único que drena (`TryDequeueDerived` interno). Honra "fila no
  processor" (seção 5) sem quebrar a construção `new CommandProcessor(context)`.
- **Alcance por Chebyshev** (8-direções, range 1 = adjacente inclui diagonal). LOS segue **fora do escopo**
  (seção 6) — ataque valida só distância + alvo inimigo.
- **Registro alimentado pelo bootstrap** via `processor.Register(piece)`. A entrada por `SummonCommand`
  (âncora de replay) é M5; a saída por morte (via `DamageCommand`) já é a definitiva. Ids começam em 1.
- **Gancho de vitória entre itens da fila documentado, vazio em M3** (como a Camada 1 foi no M2): o juiz
  que avalia vitória entre derivados nasce no M4 com o Turn System.
- **`AttackActionDefinition` com alvo por ocupante da casa.** Sem ocupante → id `default` (0), que o
  `Validate` recusa; por isso os ids de peça começam em 1 (0 = "sem alvo").

## Pendências / fora do escopo do M3

- Rodar a suíte no Test Runner do Unity (ação manual).
- Turno, stamina, `EndTurnCommand`, `CurrentPlayer`, dois controllers, **juiz de vitória + `IsVictoryCritical`** (M4).
- `SummonCommand` de setup no pipeline, golden-log de replay, `DamageSource` (lava/terreno), evento de mapa
  como derivado, gate de input na apresentação (M5).
- Highlight visual das casas de ataque (só movimento acende hoje; ataque resolve no clique).
- Feedback visual do dano não-letal (flash/número) — `DamagedEvent` já carrega o payload; a view não o usa ainda.

---

## Próximo: Marco 4 — Vira partida

Turn System (GameManager FSM, `CurrentPlayer`, `EndTurnCommand`, stamina como gate), Player com
`CurrentStamina` + cache reativo, dois controllers humanos alternando, e o **juiz de vitória** (lê a fonte
de verdade; `IsVictoryCritical` entra aqui) até a condição de fim de jogo.
