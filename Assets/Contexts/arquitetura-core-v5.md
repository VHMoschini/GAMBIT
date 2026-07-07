# Arquitetura do Core — Clone Capsule Monster Coliseum (Unity)

> Documento vivo. Consolida as decisões das sessões de planejamento + revisões de arquitetura.
> Escopo deste doc: **sistemas core (lógica)**. Visual fica desacoplado e fora daqui.
>
> **Changelog v2:** separação core↔visual reforçada por asmdef; Tile System reformulado (FSM removida, highlight virou evento com payload); nova seção Command Pipeline; FSM desacoplada do bus; bake do grid em editor-time com serialização; licença do GenericEventBus confirmada (MIT).
>
> **Changelog v2.4 (core fechado):** Turn System detalhado — turno sem fases rígidas, gated por **stamina**; `EndTurnCommand` manual explícito; duas condições de vitória via `IsVictoryCritical`. Todos os 8 sistemas desenhados.
>
> **Changelog v4 (revisão das costuras):** `SummonCommand` ganha dono + princípio do **comando autossuficiente**; checagem de vitória lê **fonte de verdade**, nunca cache via bus (novo princípio: regra não depende de estado atualizado por evento); contrato da **fila de derivados** (FIFO, sem recursão, vitória avaliada entre itens, regra de morte simultânea); princípio do **evento com fato completo** (visual nunca lê estado atual pra apresentar evento passado); recusa de comando vira `Result` retornado + `CommandRejectedEvent`; **linha de visão cortada do primeiro escopo**; `GameContext` ganha registro `GetPiece(PieceId)`; âncoras de replay no log (hash do mapa + `PieceId` determinístico); **Undo removido do horizonte de design** (não existe no jogo base nem em nenhum escopo — o log se justifica por replay/debug, não por undo); seção 2 corrigida (wizard pertence ao `HumanController`, Player não tem FSM); **gate de input no `HumanController`** (descarta clique acidental durante animação via `PresentationFinishedEvent` — input ouve o visual, core nunca).
>
> **Changelog v6 (modelo de movimento):** movimento deixa de ser alcance por flood-fill (BFS/`GetReachable`) e passa a ser **conjunto fixo de padrões de movimento por peça** (`MovePattern`). O jogador escolhe um dos movimentos cujo **caminho inteiro está livre** (deslizar — casa intermediária ocupada/bloqueada invalida o movimento; a peça não pula); se nenhum padrão tem caminho livre, a peça fica **sem jogada** (travada). **`GetReachable`/BFS removido do design de movimento** — o Grid mantém só topologia (existência da casa) + ocupação. **"Frente" é relativa ao lado do dono** (definição canônica espelhada entre os dois jogadores). Afeta seções 3, 5 e 6.
>
> **Changelog v7 (mecanismo da fronteira core→visual):** explicitado que **"core fala, visual ouve" é regra de _direção_, não de _mecanismo_.** A fronteira core→visual tem **dois mecanismos legítimos**, escolhidos pela _forma do dado_: **snapshot/estado** (topologia baked, estado inicial — "como É?") atravessa por **leitura direta** de quem é dono; **evento/delta** (move, dano, morte, highlight — "o que ACONTECEU?") atravessa pelo **event bus** com fato completo, consumido pela fila de apresentação. Ambos preservam a direção (Visual → Core; Core ignora Visual). O `GridVisualizer` lendo o `GridData` direto **não** viola a fronteira — é o caso snapshot. Afeta seção 1 (regra bus-vs-direto) e contextualiza a pendência do `GridBuiltEvent` no M1.

---

## Princípio norteador

**Core fala, visual ouve — nunca o contrário.**

O core vive em MonoBehaviours/ScriptableObjects (estilo idiomático Unity). Nenhum script de core referencia render/UI diretamente. O core muda estado e publica eventos; a camada visual escuta e reage.

**A separação é garantida por compilação, não só por disciplina:**

```
Game.Core.asmdef     → NÃO referencia Game.Visual (nem UnityEngine.UI, TMPro, etc.)
Game.Visual.asmdef   → referencia Game.Core
Game.Core.Tests.asmdef → referencia Game.Core (testes de edit mode)
```

Código de core que tentar tocar em UI/render **não compila**. Bônus: recompilação mais rápida e testes rodando só no assembly de core.

**⚠️ Novo (v4) — eventos carregam o fato completo:**

O core resolve **sincronamente e instantaneamente** — uma cadeia move→dano→morte publica três eventos em sequência imediata, e o estado do core já está "no futuro" quando o visual for animar. Por isso:

> **Todo evento carrega o fato completo do momento em que ocorreu. O visual NUNCA lê estado atual do core pra apresentar um evento passado.**

```
PieceMovedEvent  { PieceId piece, GridCoord from, GridCoord to }      // from incluso!
DamagedEvent     { PieceId target, int amount, int hpBefore, int hpAfter }
PieceDiedEvent   { PieceId piece, PlayerId owner, GridCoord at }
```

Quando a animação de movimento tocar, o `Occupant` já mudou — se o evento não carregar `from`, o visual não sabe de onde a peça saiu. Com fatos completos, o visual mantém uma **fila de apresentação** rodando atrasada em relação ao core, sem acoplamento nenhum. Sem essa regra, o primeiro encadeamento de efeitos produz animação errada e a tentação será o visual "espiar" o core — erodindo a fronteira.

---

## Sistemas e ordem de construção

Ordem ditada pelas dependências:

```
Event System  →  FSM  →  Grid  →  Tile (dados)
                  ↓                  ↓
              Piece System  →  Player State
                  ↓
   Command Pipeline + Game Rules / Turn System (orquestra tudo)
```

| # | Sistema | Status do design |
|---|---------|------------------|
| 1 | Event System | ✅ Definido (v4: + princípio do fato completo, + `CommandRejectedEvent`) |
| 2 | FSM genérica | ✅ Definido (v4: corrigido — Player não tem FSM; wizard é do `HumanController`) |
| 3 | Grid System | ✅ Definido (POCO + bake em editor) |
| 4 | Tile System | ✅ Reformulado (sem FSM; falta detalhar eventos de mapa + `TileType` SO) |
| 5 | Command Pipeline | ✅ Detalhado (v4: comando autossuficiente, fila de derivados, recusa com feedback, âncoras de replay) |
| 6 | Piece System | ✅ Detalhado (v4: `SummonCommand` com dono; LOS cortada do escopo) |
| 7 | Player / Controller | ✅ Detalhado (v4: cache `piecesInPlay` rebaixado a conveniência; gate de input no `HumanController`) |
| 8 | Game Rules / Turn System | ✅ Detalhado (v4: vitória lê fonte de verdade; morte simultânea definida) |

> **Nota:** o jogo é determinístico (comparação de stats + posicionamento, sem rolagem) → **não há sistema de RNG**. Se algo aleatório surgir depois (ex: sorteio de início), isola-se um `IRandomProvider` na ocasião.

---

## 1. Event System ✅

**Decisão:** usar a lib open source [**GenericEventBus** (PeturDarri)](https://github.com/PeturDarri/GenericEventBus) como base, com uma **camada de debug própria** por cima.

> ✅ **Licença conferida: MIT.** Pode incorporar e modificar; manter o arquivo LICENSE junto do código.

**Por quê:**
- Eventos fortemente tipados via generics (tipos, não strings) — baixo overhead em runtime.
- **Prioridade** de listeners: ordem de resolução independente da ordem de inscrição.
- **Cancelamento/consumo** de eventos e **enfileiramento** quando outro evento está sendo disparado.
- Evita escrever e debugar o bus do zero.

**Camada de debug a construir (wrapper, sem tocar na lib):**
- Logar todo `Raise` com timestamp e contagem de listeners.
- Histórico circular de eventos pra inspeção no editor.

**Disciplina obrigatória:**
- Guardar referência ao delegate pra poder cancelar inscrição.
- **Sempre** dar `Unsubscribe` em `OnDisable`/`OnDestroy`.
- **(v4)** Todo evento carrega o fato completo (ver princípio norteador) — payload com `from/to`, `hpBefore/hpAfter`, etc.

**Onde usar bus vs. chamada direta (regra prática):**
- **Bus:** fronteira core → visual **para deltas** (ver abaixo), e situações onde o emissor não deve conhecer o receptor (ex: "peça morreu" → vários sistemas reagem *de forma não-crítica*).
- **Chamada direta:** dentro do core, entre sistemas com dependência clara e onde **ordem importa** (ex: Turn System → Grid); e na fronteira core → visual **para snapshot** (ver abaixo).

**⚠️ Novo (v7) — "core fala, visual ouve" é regra de DIREÇÃO, não de MECANISMO.**

O princípio norteador fixa **quem depende de quem** (Visual → Core; o Core ignora o Visual, garantido por asmdef). Ele **não** obriga que todo dado atravesse por bus. "Ouvir" ≠ "assinar evento". A fronteira core → visual tem **dois mecanismos legítimos**, e a escolha é pela **forma do dado**, não pela camada:

| Forma do dado | Pergunta que responde | Mecanismo | Por quê |
|---|---|---|---|
| **Snapshot / estado** | "como **É** o tabuleiro/estado?" (topologia baked, estado inicial, consulta pontual) | **Leitura direta** de quem é dono | É um fato estável que já existe; o estado **precede** qualquer evento. O visual puxa quando precisa. Um evento só carregaria o mesmo dado estático no payload — cerimônia sem ganho. |
| **Evento / delta** | "o que **ACONTECEU**?" (move, dano, morte, casas a destacar) | **Event bus** com **fato completo** | Acontece no tempo; o core resolve síncrono e o visual reproduz atrasado pela fila de apresentação, sem espiar o estado já avançado (ver princípio norteador). |

**Os dois eixos são independentes:**
- **Direção** (a regra sagrada): Visual → Core, sempre. Referência direta do visual a um dado do core (ex: `GridVisualizer` lendo o `GridData`) **respeita** isso — quem conhece quem está correto.
- **Mecanismo** (pull vs. push): decidido pela forma do dado, conforme a tabela.

> **Caso concreto — Grid (M1):** o `GridVisualizer` lê o `GridData` baked **direto** (pull) e desenha a base no `Start`. Está **correto**: topologia é snapshot imutável, montada uma vez, e não há delta a reagir. É um *leitor de bootstrap*, não um listener de runtime. Quando o jogo roda, **todos** os deltas (`PieceMovedEvent`, `ReachableTilesComputedEvent`, `DamagedEvent`...) vão pelo bus — esses **nunca** podem ser leitura direta, justamente porque o visual precisa reproduzi-los atrasado. (Por isso a pendência do `GridBuiltEvent` no M1 não é dívida: tabuleiro não é delta. O bus compartilhado só nasce com o GameManager no M4; mesmo lá, a base segue sendo pull do `GridData`.)

> **Regra de bolso:** *estado que você consulta → leitura direta de quem é dono; delta que acontece no tempo → bus com fato completo.* Em ambos, a direção é a mesma e o Core nunca conhece o Visual.

> **⚠️ Novo (v4) — sentido visual → controller (não viola a fronteira):** além de core → visual, existe um único fluxo de volta legítimo: o visual avisa o *input* quando termina de animar (`PresentationFinishedEvent`), e o **`HumanController`** escuta pra reabrir o input (ver seção 7). Quem ouve é o controller (input), **nunca o core** — a regra de jogo segue sem ouvir o visual. O `Game.Core.asmdef` não referencia nada disso.

**⚠️ Novo (v4) — corolário da regra acima, agora explícito:**

> **Decisão de regra NUNCA depende de estado atualizado via bus.** Bus é pra quem *reage*; regra lê de quem é *dono*.

O GenericEventBus **enfileira** eventos disparados durante outros eventos — ou seja, um listener pode ainda não ter rodado quando código síncrono subsequente executa. Qualquer regra de jogo (vitória, validação, custo) que leia um cache atualizado por listener está lendo dado potencialmente defasado **no momento mais crítico**. Aplicação concreta na seção 8 (vitória) e na seção 7 (`piecesInPlay`).

---

## 2. FSM genérica ✅ (corrigido na v4)

**Decisão:** FSM por **composição** ("a entidade TEM uma FSM"), não por herança.

**Por quê:** entidades já serão MonoBehaviour e C# não tem herança múltipla. Composição mantém as entidades livres, permite múltiplas FSMs e torna a FSM testável isoladamente.

**Representação de estados:** abstração `IState<TContext>`, com dois jeitos de criar estados:

1. **Estado-objeto** (`IState<TContext>` com `OnEnter/OnExit/OnUpdate`) — estados com lógica rica. Uso: **GameManager** e **HumanController**.
2. **Estado-leve** (enum mapeado a `DelegateState` com callbacks opcionais) — estados triviais. Mantido na API por flexibilidade.

**Validação de transições:** ✅ obrigatória. Transições declaradas no setup; `ChangeState(destino)` inválido → **exceção**, nunca troca silenciosa.
- **Estado inicial** entra sem exigir transição.
- Suporta **`AnyState → X`** (ex: GameManager abortar pra `GameOver`).

**FSM não conhece o bus:**

A FSM é POCO e **não referencia o GenericEventBus**. Ela expõe um evento C# puro:

```
StateMachine<TContext>
  - AddState(IState<TContext>)
  - AddState(enumValue, onEnter, onExit...)
  - AddTransition(from, to)
  - AddAnyTransition(to)
  - SetInitialState(state)
  - ChangeState(to)   → valida → OnExit antigo → dispara OnStateChanged → OnEnter novo
  - Tick()
  - event Action<TState, TState> OnStateChanged   // C# puro, sem bus

  // suporte a pilha (usado pelo wizard do HumanController — ver seção 7):
  - PushState(to) / PopState()
```

A **entidade dona** decide se repassa `OnStateChanged` pro bus. Resultado: FSM testável sem bus nenhum.

**Testes desde o dia 1:** como é POCO, escrever testes de **edit mode** (Unity Test Framework) cobrindo: transição inválida lança; AnyState funciona; OnExit roda antes de OnEnter; estado inicial não exige transição; push/pop preserva coerência. Essa classe sustenta o jogo inteiro — minutos de teste, retorno permanente.

**⚠️ Corrigido (v4) — quem tem FSM:**
- **GameManager:** `Setup → DeckSelection → Playing → GameOver`
- **HumanController:** wizard de seleção em pilha (`SelectingPiece → SelectingAction → SelectingTarget`) — ver seção 7.
- ~~Player~~ → **não tem FSM.** O Player é estado puro (mão, deck, stamina); a "fase de seleção" é comportamento do controller, não do player. (A v3 ainda listava os estados de seleção no Player por resíduo da v1 — inconsistência eliminada.)
- ~~Tile~~ → não tem FSM (ver seção 4).

---

## 3. Grid System ✅

**Topologia:** grid **quadrado**. `GetNeighbors` (4/8 vizinhos) permanece como **utilitário de topologia**. ⚠️ **v6:** o movimento das peças **não** é mais decidido por vizinhança/alcance, e sim por **padrões fixos por peça** (seção 6) — o grid só responde "essa casa existe?" e "quem a ocupa?".

**Altitude:** propriedade do tile, **não** entra na coordenada. Logicamente o grid é 2D.

> **Altura quantizada:** altura armazenada como **int de "níveis"** (ex: múltiplos de 0.5 no mundo viram níveis 0, 1, 2...), quantizada no bake a partir de `transform.position.y`. Regra de bloqueio compara ints (`abs(nivelA - nivelB) > N`), nunca floats de autoria.

**Estrutura de dados — grid esparso (dicionário):**

```
GridSystem (POCO — C# puro, testável em edit mode)
  - Dictionary<GridCoord, Tile> tiles
  - GetTile(coord) → Tile | null
  - HasTile(coord) → bool
  - GetNeighbors(coord, diagonal)   // ordem FIXA de offsets (utilitário de topologia)
  // ⚠️ v6: GetReachable (BFS) removido — movimento é padrão fixo por peça (seção 6).
  //        O Grid responde só topologia (HasTile) e ocupação (GetTile().Occupant);
  //        percorrer a trajetória do movimento é da validação do comando (seção 5).

GridCoord (struct)
  - int X, Z
  - igualdade, operadores, distância

Tile (ver seção 4)
```

**GridSystem é POCO:** um MonoBehaviour fino (`GridBaker`/`GridRuntime`) carrega os dados e expõe o `GridSystem`. As queries de topologia (`GetTile`/`HasTile`/`GetNeighbors`) ficam testáveis com grids construídos à mão sem abrir cena.

**Ordem de vizinhos determinística:** `Dictionary` não garante ordem de iteração. `GetNeighbors` itera offsets em **ordem fixa** (ex: N, E, S, W, depois NE, SE, SW, NW) e **nunca** se itera o dicionário diretamente em lógica de jogo. Essencial pra replay reproduzível e pro futuro "mostrar trajeto".

**Por que esparso:** tabuleiro irregular → **buraco = ausência de chave**; `GetNeighbors` ignora o que não existe, e a validação de movimento contorna buracos naturalmente (casa inexistente na trajetória invalida o padrão).

**Authoring do mapa — bake em EDITOR-time:**
1. Artista posiciona tiles na cena; cada um tem `TileAuthoring` (MonoBehaviour) com dados de terreno.
2. **Botão de editor** coleta os `TileAuthoring`, infere `GridCoord` (`x = round(worldX / cellSize)`, `z = round(worldZ / cellSize)`), quantiza altura em níveis, **valida e serializa** o resultado (asset `GridData`).
3. Runtime **só carrega** o dado baked — sem `FindObjectsOfType` em `Awake`.
4. Bake em runtime pode existir como fallback de desenvolvimento, mas **validação roda no editor**.

> **⚠️ Novo (v4) — `GridData` versionado:** o bake grava no asset um **hash/versão do conteúdo**. O log de partida registra esse hash no cabeçalho (ver seção 5) — replay sobre mapa alterado vira **erro detectável** ("log incompatível com a versão atual do mapa") em vez de partida silenciosamente diferente.

**Validações no bake:**
- Dois tiles na mesma `GridCoord` → erro de sobreposição.
- Tile desalinhado → snap/detecção.
- Altura fora da grade de níveis → warning + quantização.

**Movimento — padrões fixos por peça (⚠️ v6: sem pathfinding):**

O jogo **não usa busca de caminho nem alcance por flood-fill**. Cada peça tem um **conjunto fixo de padrões de movimento** na sua definição (ex: "2 pra frente", "2 pra direita"). O jogador escolhe **um** padrão jogável e executa; mover = percorrer a trajetória do padrão.

- **Cada padrão é uma trajetória** (sequência ordenada de casas da origem até o destino), não um destino solto (ver `MovePattern`, seção 6).
- **Deslizar — caminho inteiro livre:** o padrão só é jogável se **todas** as casas da trajetória existem e estão livres (sem ocupante, sem bloqueio de terreno). Uma casa intermediária ocupada/bloqueada **invalida o padrão inteiro** — a peça não pula por cima.
- **"Frente" é relativa ao dono:** a definição é uma só; a trajetória é **espelhada** conforme o lado do tabuleiro do `owner`. A definição guarda o padrão canônico; o cálculo aplica o espelhamento.
- **Peça travada:** se nenhum padrão tem trajetória livre, a peça está **sem jogada de movimento** (estado derivado, recalculado quando consultado, nunca armazenado).
- **Princípio mantido — Grid fornece topologia; peça/regra fornece a política:** o Grid só responde "essa casa existe?" (`HasTile`) e "quem ocupa?" (`GetTile().Occupant`); quem percorre a trajetória e decide legalidade é o `Validate` do `MoveCommand` (seção 5), lendo os `MovePattern`s da peça.

**Integração com o resto:** as **casas-destino dos padrões jogáveis** viram **evento com payload** (`ReachableTilesComputedEvent`, ver seção 4) — o grid nunca toca em render e o tile nunca guarda "estou pintado".

---

## 4. Tile System ✅ (reformulado)

**Mudança central: o Tile NÃO tem FSM.** A versão anterior (`Empty / Occupied / Highlighted / Blocked`) tinha dois problemas:

1. **`Empty/Occupied` duplicava dado** que já existe em `Occupant` → estado derivado armazenado em dois lugares = dessincronização garantida.
2. **`Highlighted` é apresentação, não estado de jogo** — violava o "core fala, visual ouve".

**Tile reformulado — dado puro + estado derivado:**

```
Tile (POCO)
  - GridCoord Coord
  - int HeightLevel              // quantizado no bake
  - TileType Type                // ScriptableObject
  - Piece Occupant               // null se vazio
  - bool IsOccupied => Occupant != null    // DERIVADO, nunca armazenado
  - bool IsBlocked  => Type.Blocks...      // vem do TileType
```

**Highlight vira evento com payload:**

```
ReachableTilesComputedEvent { PlayerId player, IReadOnlyCollection<GridCoord> tiles }
ReachableTilesClearedEvent  { PlayerId player }

// camada visual escuta e pinta/despinta. O core não sabe o que é "pintado".
// ⚠️ v6: 'tiles' são os DESTINOS dos padrões de movimento jogáveis (caminho livre),
//        não o alcance de um BFS. O significado do evento (casas a destacar) não muda.
```

**Falta detalhar (próxima sessão):**
- **Eventos de mapa** — hipótese de trabalho: evento de mapa = **comando derivado enfileirado no pipeline** (ex: `MoveCommand` resolvido em tile de lava → fila recebe `DamageCommand` via `SubmitSystem`). Validar com casos concretos do jogo base.
- **`TileType` (ScriptableObject):** o que cada terreno carrega — bloqueio, diferença de altura que bloqueia, gatilhos de evento de mapa.

---

## 5. Command Pipeline ✅ (revisado na v4)

**Decisão:** toda mutação de estado de jogo passa por um pipeline **Intent → Validação → Comando → Resolução**. Nenhum sistema altera estado de partida diretamente; todos os produtores (input, IA futura, efeitos de tile, regras de turno) submetem comandos, e o `CommandProcessor` é o **único** ponto que aplica mutação.

**Por quê (jogo determinístico por turnos é o caso ideal):**
- **Log/replay de graça:** a partida inteira é uma lista serializada de comandos. Debug de "como chegou nesse estado?" = reexecutar o log.
- **IA futura sem reescrever nada:** IA é só outro produtor de comandos.
- **Testes de regra puros:** "dado este estado, este comando é válido?" — sem cena, sem MonoBehaviour.

> **⚠️ Revisado (v4) — Undo: fora do design, não só do escopo.** Undo não existe no jogo base e não está em nenhum escopo planejado. **Nenhuma decisão de design é tomada "pra viabilizar undo".** O log de comandos se justifica sozinho por replay/debug. (A nota da v2.1 que condicionava o design à promessa "log suficiente pra reconstruir qualquer estado em nome do undo futuro" está revogada; as âncoras de determinismo abaixo existem pelo replay/debug, que é benefício declarado do pipeline.)

### As quatro fases

1. **Intent** — o que o produtor *quer* fazer, cru e ainda não validado. Vem tipicamente do controller. Não sabe se é legal.
2. **Validação** — converte intent em "comando válido ou recusa motivada". Resultado é um `Result` (ok | motivo). **Nada mutou ainda.**
3. **Comando** — objeto tipado, validado e **imutável**, pronto pra aplicar. É o que entra no log.
4. **Resolução** — `Execute` aplica a mutação (via sistemas donos), registra no log e **publica eventos de resultado** no bus (com fato completo). Pode enfileirar comandos derivados.

### ⚠️ Novo (v4) — Princípio do comando autossuficiente

> **Todo comando carrega explicitamente tudo que seu `Validate` e `Execute` precisam. Nunca infere do contexto ambiente (`CurrentPlayer`, fase atual, etc.).**

Motivação dupla: (a) o `SummonCommand` do setup roda **fora de qualquer turno** — `CurrentPlayer` não significa nada ali; sem campo `owner`, o `Execute` não sabe de quem é a peça (bug concreto da v3); (b) o comando no log deve ser legível e re-executável por si só. Consequências:

```
SummonCommand  { PlayerId owner, PieceDefId def, GridCoord at }   // owner EXPLÍCITO
EndTurnCommand { PlayerId player }                                 // quem encerrou fica no log
```

O contexto continua existindo pra *consulta de regra* (a pré-checagem global compara `command.owner` com `CurrentPlayer`) — mas o dado do comando vive **no comando**.

### Validação em duas camadas

Regras separadas **por escopo**, não por comando:

- **Camada 1 — pré-checagens globais, no `CommandProcessor`.** Invariantes que valem pra *qualquer* comando: é o turno do player que submeteu? a partida aceita comandos agora? Existem **uma vez**, não duplicadas por comando.
- **Camada 2 — `Validate` específico, no comando.** Cada comando valida só o que é dele: `MoveCommand` checa posse, que o destino é o de um **padrão de movimento da peça com caminho inteiro livre** (deslizar) e que o tile final está livre; `AttackCommand` checa alcance e alvo inimigo.

### ⚠️ Novo (v4) — Recusa com feedback

Um comando recusado não pode morrer em silêncio — a UI precisa do motivo ("sem stamina", "fora de alcance"):

```
Submit(command) → Result            // retornado ao chamador (controller decide o que fazer)
                + CommandRejectedEvent { command, motivo }   // publicado no bus pro visual reagir
```

**Recusa também é informação que o visual apresenta.** O `Result` retornado serve o fluxo síncrono do controller; o evento serve feedback visual desacoplado (shake, som de erro, tooltip).

### GameContext — fachada de acesso ao estado distribuído

**Decisão:** não há `GameState` central monolítico. O estado mora nos sistemas donos. O comando recebe um **`GameContext`**: ponto de acesso aos donos, **não** saco de estado.

```
GameContext (passado a Validate e Execute)
  - GridSystem Grid                  // dono dos tiles e da ocupação
  - IReadOnlyList<Player> Players
  - Player CurrentPlayer             // mantido pelo Turn System
  - Piece GetPiece(PieceId id)       // ⚠️ NOVO (v4): registro de peças vivas
```

> **⚠️ Novo (v4) — registro `PieceId → Piece`:** os comandos carregam IDs (correto pro log), mas alguém precisa resolver ID em objeto. Nem Grid (só conhece posicionadas) nem Player (só conhece a mão) cobrem tudo. O **`CommandProcessor` mantém o registro**: peça entra no registro quando um `SummonCommand` resolve, sai quando morre. `GetPiece` é a porta única — sem isso, cada `Validate` inventaria uma resolução ad hoc.

### ⚠️ Novo (v4) — Comandos derivados: contrato da fila

Quando um `Execute` gera um comando derivado (lava → `DamageCommand`, contra-ataque, etc.), três regras valem:

1. **Fila FIFO, nunca recursão.** O derivado **não** executa inline dentro do `Execute` pai — entra numa fila no processor, processada iterativamente **depois** que o `Execute` pai termina. Evita reentrância no processor e garante que os eventos cheguem ao visual na ordem real de resolução (move → dano → morte, nunca aninhado).
2. **Derivado pula a Camada 1 de turno, mantém o resto.** Efeito não é "o turno de ninguém" — é consequência de um comando já legitimado. Mas o `Validate` específico **roda sempre**: um contra-ataque enfileirado cujo atacante morreu no item anterior da fila é recusado naturalmente ("atacante não está vivo").
3. **Vitória é avaliada entre cada item da fila** (ver seção 8). Se a partida terminou no meio da cadeia, a pré-checagem "partida não acabou" recusa o restante da fila — a cadeia morre sozinha, sem código especial de drenagem.

```
Submit(MoveCommand)
  → Execute move → enfileira DamageCommand (lava)
  → [juiz avalia vitória]                        // ninguém morreu, segue
  → fila: DamageCommand → Validate → Execute → peça morre
  → [juiz avalia vitória]                        // peça-chave? → GameOver
  → fila: comandos restantes recusados ("partida encerrada")
```

### Forma (consolidada)

```
ICommand
  - Validate(GameContext) → Result (ok | motivo da recusa)
  - Execute(GameContext)  → muta via sistemas donos + publica eventos (fato completo) no bus

CommandProcessor
  - Submit(command)        → JOGADA: pré-checagens globais (inclui turno) → Validate → Execute
                           → log → CommandResolvedEvent → processa fila de derivados
  - SubmitSystem(command)  → SISTEMA (setup/efeitos): pula validação de turno,
                           → demais checagens globais → Validate → Execute → log → fila
  - Submit retorna Result; recusa publica CommandRejectedEvent
  - Registry: PieceId → Piece (alimentado por Summon, limpo por morte)
  - History (lista serializável — partida inteira, setup incluído)
```

### ⚠️ Novo (v4) — Âncoras de replay no log

Pro log ser re-executável (debug/replay), dois pré-requisitos baratos:

1. **Cabeçalho do log registra o hash/versão do `GridData`** (ver seção 3). Mapa alterado → erro explícito, não partida silenciosamente diferente.
2. **`PieceId` é sequencial, atribuído pelo `CommandProcessor` na ordem de invocação.** Como a ordem de invocação está no log, o replay regenera os mesmos IDs por construção. **Nunca** GUID aleatório nem ID de instância do Unity — esses quebram toda referência do log no replay.

**Teste recomendado (opcional, alto valor):** golden-log em edit mode — serializa o log de uma partida curta, re-executa do zero, compara o estado final. É o teste que acusa no dia em que alguém quebrar o determinismo sem perceber.

### Fluxo completo de um movimento

```
Input (controller) → MoveCommand → CommandProcessor.Submit
  → [Camada 1] pré-checagens globais (turno via command.owner vs CurrentPlayer, fase)
  → [Camada 2] MoveCommand.Validate (posse, padrão de movimento com caminho livre, ocupação)
  → Execute (Grid.ClearOccupant/SetOccupant, registra no log)
  → publica PieceMovedEvent { piece, from, to } no bus
  → visual enfileira animação (lendo o payload, não o estado)
  → eventos de mapa enfileiram derivados na fila se houver
```

**Conexão com o Piece System:** o SO da ação descreve parâmetros; executar a ação = emitir o comando tipado. Dados e execução separados.

**Conexão com Player/Controller:** controller (humano ou bot) = qualquer coisa que produz comandos.

---

## 6. Piece System ✅ (revisado na v4)

**Princípio central: definição vs. instância.** A definição é o molde imutável (Inspector); a instância é o objeto vivo. A instância **referencia** a definição, nunca duplica.

**`PieceDefinition` (ScriptableObject) — o molde, 1 por tipo (10 no total):**

```
PieceDefinition (ScriptableObject)
  - string DisplayName
  - int Attack
  - int Defense
  - List<MovePattern> MovePatterns   // ⚠️ v6: conjunto FIXO de movimentos (ex: "2 pra frente")
  - int MaxHP
  - bool IsVictoryCritical     // o "rei": se morrer, o dono perde
  - List<ActionDefinition> Actions
  // sem referência a sprite/visual aqui
  // ⚠️ v6: MoveRange/MovesDiagonally REMOVIDOS — substituídos por MovePatterns.
```

> **Atributos fixos:** campos concretos tipados, não dicionário de stats → editável no Inspector, validável, sem typo de chave.

**`MovePattern` — um movimento canônico da peça (⚠️ v6):**

```
MovePattern
  - IReadOnlyList<GridCoord> Steps   // trajetória relativa à origem, na ORIENTAÇÃO CANÔNICA
                                     // ex: "2 pra frente" = [(0,1),(0,2)]
  - string Label                     // rótulo opcional pra UI ("2 pra frente")
```

- **Trajetória, não destino solto:** `Steps` lista as casas atravessadas, em ordem, até o destino (último item). É o que viabiliza a regra de **deslizar** (caminho inteiro livre — seção 3).
- **Espelhamento por dono:** a trajetória é **canônica**; ao avaliar, aplica-se o espelhamento conforme o lado do `owner` ("frente" relativa ao dono — seção 3). Uma definição só serve aos dois jogadores.
- **Movimentos compostos:** uma trajetória em L ("2 à frente, depois 2 à direita") é só uma lista de `Steps` mais longa — o mesmo modelo cobre retas e curvas.
- **Peça travada:** se nenhum `MovePattern` tem trajetória inteiramente livre, a peça não tem jogada de movimento (derivado, recalculado, nunca armazenado).

**`Piece` (instância) — só o estado mutável:**

```
Piece (instância em jogo)
  - PieceDefinition Definition
  - PieceId Id                    // sequencial, atribuído pelo processor (ver seção 5)
  - PlayerId Owner
  - GridCoord Position
  - int CurrentHP
```

`CurrentHP` é o único stat que "duplica" a definição — justificado porque *diverge* em jogo. Se buffs surgirem, entra camada de modificadores (`EffectiveAttack = Definition.Attack + mods`), **sem** sobrescrever a base.

### Ações configuráveis — subtipos de ActionDefinition (mapeamento 1:1 com comando)

**Decisão:** cada ação é um **subtipo concreto** de `ActionDefinition` (SO abstrato), e cada um sabe construir seu comando tipado. Subtipo carrega seus próprios parâmetros tipados no Inspector; polimorfismo resolve o dispatch; ação nova = nova classe + asset.

```
ActionDefinition (ScriptableObject, abstrato)
  - string DisplayName
  - TargetType Targeting        // Self / Tile / EnemyPiece / ...
  - int StaminaCost
  - abstract ICommand BuildCommand(Piece source, target, GameContext ctx)

MoveActionDefinition : ActionDefinition
  // ⚠️ v6: o "alvo" é um MovePattern escolhido (ou sua casa-destino), não uma casa
  //        livre qualquer de um alcance. BuildCommand resolve o destino pela trajetória.
  - BuildCommand(source, chosenPattern, ctx) → new MoveCommand { ..., to = destino do padrão }

AttackActionDefinition : ActionDefinition
  - int Range                   // alcance por DISTÂNCIA DE GRID (ver nota de escopo)
  - BuildCommand(...) → new AttackCommand { ... }
```

> **⚠️ Novo (v4) — linha de visão CORTADA do primeiro escopo.** A v3 mencionava "linha de visão" no `Validate` do `AttackCommand` sem que nenhum sistema soubesse calculá-la — LOS em grid com altura é um problema de design próprio (algoritmo de traçado, regras de quina, oclusão por nível). **No primeiro escopo, ataque valida só distância de grid + alvo inimigo.** Se o jogo base exigir LOS, vira decisão em aberto com sessão de design dedicada — não entra de contrabando por uma menção em pseudocódigo.

**Fluxo:**

```
Player seleciona peça → escolhe ação (de Definition.Actions)
  → escolhe alvo (guiado por Targeting)
  → ActionDefinition.BuildCommand(...) produz o ICommand tipado
  → CommandProcessor.Submit(command)
  → [pipeline: Camada 1 → Validate → Execute → eventos]
```

> **Targeting ≠ Validação.** `TargetType` serve o controller na seleção (UX); o `Validate` revalida sempre, porque o controller pode estar enganado ou o estado pode ter mudado.

### Invocação de peças — setup inicial + durante a partida

Ambos usam `SummonCommand { owner, def, at }` (dono explícito — seção 5). A diferença é a **porta**:

- **Durante a partida** (jogada): `Submit` → duas camadas completas (turno, posse na mão, tile válido e livre). O `Execute` **remove a peça da mão** do owner e a registra no Grid + registry.
- **Setup inicial** (sistema): `SubmitSystem` → pula validação de turno, mantém `Validate` específico + `Execute`.

**Arranjo B mantido: setup roda via `SummonCommand`s de sistema, dentro do pipeline.** O log contém a partida inteira — do primeiro posicionamento ao último movimento — e o replay reconstrói tudo re-executando o log (justificado por replay/debug; ver nota sobre Undo na seção 5).

---

## 7. Player / Controller ✅ (revisado na v4)

**Separação central: o que o player É (estado) vs. quem decide pelo player (controller).** Multiplayer **local** = dois `Player` + dois controllers, alternando turno.

**`Player` (estado) — o que o player possui:**

```
Player
  - PlayerId Id
  - mão/deck                   // peças NÃO-posicionadas (o Grid não as conhece)
  - List<Piece> piecesInPlay   // cache de CONVENIÊNCIA (ver revisão abaixo)
  - int CurrentStamina         // recurso de turno; recarrega na troca de turno
```

> **Fronteira de propriedade:** o Grid é dono do que está **posicionado** (via `Occupant`); o Player é dono do que **ainda não foi** (mão/deck). O **registro `PieceId → Piece` do processor** (seção 5) é o índice global de peças vivas.

> **⚠️ Revisado (v4) — `piecesInPlay` rebaixado a cache de conveniência.** A v3 fazia a checagem de vitória ler esse cache, atualizado por listener de `SummonedEvent`/`PieceDiedEvent` no bus. Problema: o bus **enfileira** eventos durante eventos — não há garantia de que o listener rodou quando o juiz checa sincronamente após o `Submit`. Vitória decidida por cache potencialmente defasado = bug intermitente irreproduzível. Correção pelo princípio da seção 1 (*regra nunca lê estado atualizado via bus*): o cache **continua existindo pra queries de UI/conveniência** (onde 1 frame de defasagem é irrelevante), mas **nenhuma regra de jogo lê dele** — a checagem de vitória lê a fonte de verdade (seção 8).

**`IController` (decisão) — produtor de comandos:**

```
IController (associado a um Player)
  - produz comandos quando é o turno do seu player
  HumanController : seleção da UI → BuildCommand → CommandProcessor.Submit
  AIController (futuro) : avalia estado → BuildCommand → Submit
```

Ambos plugam no mesmo `Submit`. O Turn System e o pipeline **não distinguem** humano de IA. O `Result` retornado pelo `Submit` informa o controller sobre recusas (o `HumanController` reage via UI; um futuro `AIController` reavalia).

### Wizard de seleção — mora no HumanController

A FSM de seleção (`SelectingPiece → SelectingAction → SelectingTarget`) pertence ao **`HumanController`**, porque seleção é *como um humano produz um comando*. Um `AIController` não tem wizard. O `Player` é estado puro, sem FSM (corrigido na seção 2).

**Estado parcial em `SelectionContext` separado (não dentro da FSM):**

```
SelectionContext (efêmero, vive durante a montagem de uma jogada)
  - Piece            // peça escolhida
  - ActionDefinition // ação escolhida
  - target           // alvo (pendente até o último passo)
```

A FSM controla *a fase*; o `SelectionContext` guarda *o que foi montado*. Ao completar, `ActionDefinition.BuildCommand` lê o contexto e produz o comando; ao submeter ou cancelar, o contexto reseta.

**Pilha de estados (push/pop) pro wizard:** avançar = push; voltar/cancelar = pop. Pop da pilha = desfazer o último campo do `SelectionContext`. Cancelar tudo = esvaziar a pilha + limpar o contexto.

### ⚠️ Novo (v4) — Gate de input durante a apresentação visual

**Problema:** o core resolve sincronamente; o visual reproduz atrasado via fila de apresentação. Durante uma animação, um clique acidental do jogador não pode virar um comando indesejado.

**Decisão: o gate é de INPUT, no `HumanController` — não de regra, não no core.** Um clique só vira comando quando o controller decide construir e submeter. Logo, a barreira fica *antes do clique virar comando*: enquanto a apresentação está rolando, o `HumanController` não traduz cliques em seleção. Quando o visual termina, reabre.

```
// o visual avisa quando sua fila de apresentação esvazia:
PresentationFinishedEvent { }

// o HumanController escuta e reabre o input. O CORE não escuta isso.
HumanController:
  - aceitaInput = false  enquanto apresentação roda
  - ao receber PresentationFinishedEvent → aceitaInput = true
  - clique com aceitaInput == false → DESCARTADO (não bufferiza)
```

> **Isto NÃO é o core ouvindo o visual.** Quem escuta `PresentationFinishedEvent` é o `HumanController` — input, não core. O `Game.Core.asmdef` não referencia nada de visual. A fronteira sagrada ("regra de jogo não ouve visual") é sobre *lógica de jogo*; input ouvir apresentação é a função natural do controller, que é a ponte humano↔core. A assimetria que mantém tudo limpo:
> - **Core → visual:** core publica fatos, visual reage.
> - **Visual → controller:** visual avisa "terminei", controller de input reage.

> **⚠️ Por que NUNCA pôr isto na validação de comando:** se o `Validate` consultasse um `visualReady`, a regra de jogo passaria a depender de frame rate, velocidade de animação e do replay (que roda sem visual) — quebrando o determinismo e o golden-log. É exatamente o anti-padrão da seção 1 (*regra nunca lê estado atualizado via bus*). O gate de input não tem esse problema porque é assimétrico em consequência:

**O gate é conforto (UX), não correção.** Se ele falhar (evento perdido, reabre cedo), o pior caso é um clique passar durante a animação — e esse clique **vira um comando que o pipeline valida normalmente**: se for sem sentido, o `Validate` recusa (com `CommandRejectedEvent` pro feedback); se for válido, executa sem corromper nada. A correção da partida nunca depende do gate. (Contraste: gate na validação corromperia a resolução se falhasse.)

> **Escopo:** clique durante o bloqueio é **descartado** (não bufferizado) — bufferizar guardaria justamente o clique acidental que se quer evitar. Buffer do último input fica como evolução futura, se a UX pedir.

---

## 8. Game Rules / Turn System ✅ (revisado na v4)

**Orquestrador.** Quatro responsabilidades:

1. **Dono do GameManager FSM** — `Setup → DeckSelection → Playing → GameOver`, com `AnyState → GameOver`.
2. **Maestro do setup** — dispara a leva de `SummonCommand`s de sistema (com `owner` explícito) via `SubmitSystem`; ao terminar, transiciona pra `Playing`.
3. **Controlador de turno** — mantém `CurrentPlayer` (exposto no `GameContext`) e alterna entre os players.
4. **Juiz de fim de jogo** — avalia vitória/derrota e leva o GameManager pra `GameOver`.

### Estrutura do turno — stamina como recurso, sem fases rígidas

**Decisão:** turno = janela em que o `CurrentPlayer` submete comandos livremente até mandar `EndTurnCommand`. **Sem fases fixas** — o gate é a **stamina**.

- **Custo mora na ação** (dado): `StaminaCost` na `ActionDefinition`.
- **Saldo mora no Player** (estado): `CurrentStamina`, recarregado na troca de turno.
- **Checagem no pipeline:** antes do `Execute`, o processor confirma `player.CurrentStamina >= command.StaminaCost`; o `Execute` debita.

> **Ordem do débito:** stamina é debitada no `Execute`, após validação. Comandos via `SubmitSystem` (setup, derivados) **não debitam stamina** — a porta de sistema já isola isso naturalmente.

### End Turn — comando como qualquer outro

**Decisão:** encerramento **manual explícito** via `EndTurnCommand { player }`. Passa pelo pipeline; seu `Execute` pede a troca ao Turn System (dono do turno), que recarrega a stamina do próximo, atualiza `CurrentPlayer` e publica `TurnChangedEvent`. Entra no log com o autor registrado.

> **Decisão de escopo consciente — encerramento por falta de stamina:** no primeiro escopo, o turno só passa com `EndTurnCommand` manual, mesmo sem stamina. Evolução futura registrada: Turn System detecta "nenhuma ação pagável" → evento de aviso + auto-submete `EndTurnCommand`. Fora do escopo atual.

### ⚠️ Revisado (v4) — Condições de vitória: lendo a fonte de verdade

**Decisão:** vitória se (a) o oponente perde **todas** as peças, OU (b) o oponente perde a **peça-chave** (`IsVictoryCritical` na `PieceDefinition`). Ambas são consequência de morte → avaliadas **após cada comando resolvido** — incluindo **entre cada item da fila de derivados** (seção 5).

**De onde o juiz lê (correção da v3):** da **fonte de verdade**, não do cache `piecesInPlay`. Concretamente, o juiz consulta o registro de peças vivas do processor (ou varre as peças do player — são ≤ 20 peças em jogo, varredura é trivial). O ganho de O(1) do cache não paga o risco de decidir a partida com dado defasado pelo enfileiramento do bus. *(Justificativa completa nas seções 1 e 7.)*

> **⚠️ Novo (v4) — morte simultânea (regra de desempate):** as duas peças-chave podem morrer na **mesma cadeia de derivados** (contra-ataque mútuo, lava dupla). Como a vitória é avaliada após cada comando, sem regra explícita a *ordem da fila* decidiria o vencedor arbitrariamente. **Regra adotada:** a avaliação após cada comando considera o estado de **ambos** os players; se ambos perderam a condição de vitória no mesmo comando, é **empate** (`GameOver` com resultado de empate). Se as mortes ocorrem em comandos diferentes da cadeia, vence quem ainda estava vivo quando o oponente caiu — a fila restante é recusada pela checagem "partida encerrada". *(Confirmar contra o comportamento do jogo base quando houver acesso; a regra está isolada no juiz e custa uma linha pra mudar.)*

### Comandos do core (consolidado, v4)

```
// jogada (Submit — respeita turno + stamina):
MoveCommand    { PlayerId owner, PieceId piece, GridCoord from, GridCoord to }
AttackCommand  { PlayerId owner, PieceId attacker, PieceId target }
SummonCommand  { PlayerId owner, PieceDefId def, GridCoord at }
EndTurnCommand { PlayerId player }

// sistema (SubmitSystem — pula turno/stamina):
SummonCommand  { PlayerId owner, PieceDefId def, GridCoord at }   // setup inicial
DamageCommand  { PieceId target, int amount, DamageSource source } // efeito derivado
```

**Core fechado.** Todos os 8 sistemas desenhados, costuras revisadas. Próximo passo: implementação/milestones.

---

## Decisões em aberto pra próxima sessão

**O core está estruturalmente fechado e com as costuras revisadas.** O que resta:

1. **Tile System — detalhe final:** confirmar "evento de mapa = comando derivado na fila" com casos concretos do jogo base e estruturar `TileType` (SO). Único ponto do core ainda parcial.
2. **Morte simultânea:** confirmar a regra de empate contra o comportamento do jogo base (a regra adotada na seção 8 é hipótese isolada no juiz).
3. **Planejamento de implementação:** milestones, tasks, estimativas e time tracking pras sessões de live coding. A cadeia de dependências já dá a ordem.

> **Resolvido na v4:** comando autossuficiente (`SummonCommand` com owner); vitória lê fonte de verdade (cache rebaixado a conveniência); contrato da fila de derivados (FIFO, vitória entre itens, empate definido); eventos com fato completo; recusa com `Result` + `CommandRejectedEvent`; LOS cortada do escopo; registro `GetPiece` no `GameContext`; âncoras de replay (hash do mapa, `PieceId` sequencial); Undo removido do horizonte de design; FSM do Player eliminada da seção 2 (wizard é do `HumanController`).
>
> **v2.4:** Turn System (stamina sem fases, `EndTurnCommand`, 2 condições de vitória). **v2.3:** Player/Controller. **v2.2:** Piece System. **v2.1:** validação em duas camadas, `GameContext`, derivados.

---

## Glossário rápido

- **POCO:** Plain Old C# Object — classe C# pura, sem dependência de MonoBehaviour.
- **BFS:** Breadth-First Search — busca em largura. ⚠️ v6: **removido do design de movimento** (substituído por `MovePattern`s fixos por peça); fica só como referência conceitual.
- **Grid esparso:** grid representado por dicionário (só células existentes).
- **Auto-bake:** processo (de editor) que infere o grid lógico a partir das posições dos tiles na cena e serializa o resultado (com hash de versão).
- **`canStep`:** delegate/regra que decide se uma casa pode ser pisada ao percorrer a trajetória de um `MovePattern` (existência da casa + ocupação + terreno). ⚠️ v6: usado na checagem de **caminho livre**, não mais num BFS.
- **`MovePattern` (padrão de movimento):** trajetória canônica e fixa de uma peça (ex: "2 pra frente" = `[(0,1),(0,2)]`); a peça tem um conjunto deles. Substitui o alcance por BFS (v6).
- **Deslizar (caminho livre):** um `MovePattern` só é jogável se TODAS as casas da trajetória existem e estão livres; casa intermediária ocupada/bloqueada invalida o movimento (a peça não pula).
- **Frente relativa ao dono:** a trajetória do `MovePattern` é canônica e espelhada conforme o lado do tabuleiro do `owner`; uma definição serve aos dois jogadores.
- **Peça travada:** peça cujos `MovePattern`s não têm nenhuma trajetória livre no momento; estado derivado, recalculado, nunca armazenado.
- **asmdef:** Assembly Definition — referências entre assemblies declaradas, transformando regra arquitetural em erro de compilação.
- **Command Pipeline:** toda mutação de estado passa por comandos validados e logados (Intent → Validação → Comando → Resolução); habilita replay/debug e testes puros.
- **Estado derivado:** informação calculada de outro dado (`IsOccupied => Occupant != null`) em vez de armazenada em paralelo.
- **Definição vs. instância:** `PieceDefinition` (SO, molde imutável) vs. `Piece` (objeto vivo, só estado mutável).
- **GameContext:** fachada passada aos comandos que dá acesso aos sistemas donos do estado + registro `GetPiece(PieceId)`; não é dono de estado.
- **Comando de jogada vs. de sistema:** a distinção é a **porta de submissão** (`Submit` vs. `SubmitSystem`), não o tipo do comando.
- **Comando autossuficiente:** todo comando carrega explicitamente o que seu Validate/Execute precisam (ex: `owner`); nunca infere do contexto ambiente. O log fica legível e re-executável por si.
- **Fato completo (evento):** payload de evento carrega tudo do momento do ocorrido (`from/to`, `hpBefore/hpAfter`); o visual nunca lê estado atual do core pra apresentar evento passado.
- **Snapshot vs. delta (fronteira core→visual):** dois mecanismos legítimos na mesma fronteira. **Snapshot** = estado estável que já existe ("como É?", ex: topologia baked) → **leitura direta** de quem é dono. **Delta** = algo que aconteceu no tempo ("o que ACONTECEU?", ex: move/dano/morte) → **bus** com fato completo. A direção (Visual → Core) é a mesma nos dois; só muda o mecanismo (pull vs. push). "Core fala, visual ouve" é regra de direção, não de mecanismo.
- **Fila de derivados:** comandos gerados por um `Execute` entram em fila FIFO processada iterativamente (nunca recursão); pulam validação de turno mas mantêm `Validate` específico; vitória avaliada entre itens.
- **Cache reativo (de conveniência):** lista derivada mantida por eventos do bus (ex: `piecesInPlay`); serve UI/queries onde defasagem de 1 frame é irrelevante. **Regra de jogo nunca lê de cache reativo** — lê da fonte de verdade.
- **SelectionContext:** objeto efêmero que acumula a jogada em montagem enquanto a FSM (pilha) do `HumanController` controla a fase.
- **Player vs. Controller:** `Player` é estado (mão, deck, stamina, sem FSM); `IController` é decisão (produz comandos).
- **Stamina:** recurso de turno (`CurrentStamina`) consumido por ação (`StaminaCost`); substitui fases de turno. Comandos de sistema não debitam.
- **`IsVictoryCritical`:** booleano na `PieceDefinition` que marca a peça-chave; sua morte encerra o jogo. Juiz lê da fonte de verdade, após cada comando (incluindo itens da fila).
- **`EndTurnCommand`:** comando de jogada (`{ player }`) que passa a vez via Turn System; encerramento manual explícito no primeiro escopo.
- **Golden-log (teste):** teste de edit mode que re-executa um log de partida gravado e compara o estado final — guarda-chuva do determinismo do replay.
- **Gate de input:** trava no `HumanController` que descarta cliques durante a animação (escuta `PresentationFinishedEvent` do visual pra reabrir). É UX, não correção — clique que escapa vira comando que o pipeline valida normalmente. Input ouve o visual; o core nunca.
- **`PresentationFinishedEvent`:** evento publicado pelo visual quando sua fila de apresentação esvazia; ouvido pelo `HumanController` (input), nunca pelo core.
