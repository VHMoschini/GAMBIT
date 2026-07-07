# Progresso — Marco 1: Grid que se vê

> Sessão de 2026-06-17. Sistemas entregues: **Grid System + Tile (como dado) + bake editor-time**.
> Companion: `arquitetura-core-v5.md` (seções 3 e 4) e `ordem-desenvolvimento.md` (Marco 1).

> ⚠️ **Revisão de design (2026-06-18) — movimento mudou de paradigma.** O modelo de **alcance por BFS (`GetReachable`)** entregue originalmente neste marco foi **substituído** por **padrões fixos de movimento por peça** (`MovePattern`: deslizar com caminho inteiro livre; frente relativa ao dono) — ver `arquitetura-core-v5.md`, changelog v6 (seções 3, 5 e 6). Consequência aplicada ao código: **`GridSystem.GetReachable` e os testes de BFS (`GetReachable_*`, incluindo `GetReachable_BfsDetoursAroundHole`) foram REMOVIDOS** nesta revisão, junto com as menções a BFS/`canStep` nos comentários. O resto do M1 (grid esparso, `GetTile`/`HasTile`/`GetNeighbors`, bake, `GridData`, visualizer, demais testes) **permanece válido** — o Grid segue dono da topologia e da ocupação, que é o que o novo modelo de movimento consulta.

---

## Entregável do marco

> *"Abrir a cena, ver o tabuleiro irregular montado (com buraco), e um teste provando que o BFS contorna o buraco."*
>
> ⚠️ A meta original citava o BFS. Com a revisão v6 (movimento = padrões fixos por peça), **o BFS foi removido**; o entregável visual do marco (tabuleiro irregular na tela) e a topologia do grid permanecem.

- ✅ **Tabuleiro irregular num clique:** menu `GAMBIT/Grid/Create Demo Grid` gera um tabuleiro 7×7 com buraco e pedras intransponíveis, já visível como cubos coloridos na cena.
- ✅ **Bake editor-time:** menu `GAMBIT/Grid/Bake Scene To GridData` serializa a cena num asset `GridData` versionado.
- ✅ **Renderização desacoplada:** `GridVisualizer` (camada Visual) lê o `GridData` baked e desenha o tabuleiro ao dar Play — "core fala, visual ouve".
- ❌ ~~**Teste do BFS contornando o buraco:** `GridSystemTests.GetReachable_BfsDetoursAroundHole`.~~ **Removido na revisão v6** (movimento deixou de ser BFS — ver nota no topo). O desvio em torno do buraco passa a ser propriedade dos `MovePattern` (caminho livre), validado no M2.

---

## O que foi construído

### Core — lógica pura, testável (`Game.Core`, namespace `Game.Core.Grid`)

| Arquivo | Papel |
|---------|-------|
| `Grid/GridCoord.cs` | Struct serializável (X, Z) — igualdade/hash por valor (chave de dicionário), operadores +/-, distâncias Manhattan/Chebyshev e **offsets de vizinhança em ordem fixa** (N,E,S,W + NE,SE,SW,NW). 2D por princípio: altura não entra na coordenada. |
| `Grid/TileType.cs` | `ScriptableObject` de terreno: `DisplayName`, `DebugColor` (placeholder visual), `BlocksMovement`. `[CreateAssetMenu]` em `GAMBIT/Tile Type`. |
| `Grid/Tile.cs` | POCO dado puro: `Coord`, `HeightLevel`, `Type`, `IsBlocked` **derivado** de `TileType`. Sem FSM, sem highlight. |
| `Grid/GridSystem.cs` | Grid esparso (`Dictionary<GridCoord,Tile>`): `GetTile`/`HasTile`, `GetNeighbors` (ordem fixa, ignora buracos). Lança em sobreposição. **⚠️ v6: `GetReachable` (BFS/`canStep`) foi REMOVIDO** — movimento agora é padrão fixo por peça (ver nota no topo). |
| `Grid/GridData.cs` | `ScriptableObject` baked: lista de `BakedTile` + `cellSize`/`heightStep` + **`ContentHash`** (âncora de versão p/ replay). `CreateGridSystem()` reconstrói o POCO. |
| `Grid/TileAuthoring.cs` | MonoBehaviour de autoria na cena (só dado de terreno; lido em editor-time). |
| `Grid/GridRuntime.cs` | MonoBehaviour fino: **só carrega** o `GridData` e expõe o `GridSystem`. Sem `FindObjectsOfType` em runtime. |

### Editor (`Game.Core.Editor`, novo asmdef)

| Arquivo | Papel |
|---------|-------|
| `Editor/GridBaker.cs` | Bake da cena: coleta `TileAuthoring`, infere `GridCoord` (`round(pos/cellSize)`), quantiza altura em níveis, **valida sobreposição** (aborta com diálogo) e alinhamento (warning), serializa no `GridData`. |
| `Editor/GridDemoBuilder.cs` | Gera o tabuleiro de demo com buraco e cria os `TileType` (`Grass`, `Rock`) em `Assets/GridDemo`. |

### Visual (`Game.Visual`, namespace `Game.Visual.Grid`)

| Arquivo | Papel |
|---------|-------|
| `Grid/GridVisualizer.cs` | Lê o `GridData` (direto ou via `GridRuntime`) e instancia um placeholder por tile, colorido pelo `TileType.DebugColor`. Demonstra a fronteira: o visual lê o dado do core; o core não conhece render. **Referência de autoria:** `OnDrawGizmos` desenha a grid (contorno + preenchimento translúcido + rótulo de coordenada opcional) no Scene view **fora do Play**, sem instanciar objetos — para o artista compor o cenário sobre a footprint do tabuleiro. |

### Testes de edit mode (`Game.Core.Tests`)

- `GridCoordTests.cs` — igualdade/hash, uso como chave de dicionário, aritmética, distâncias, **ordem fixa dos offsets**.
- `GridSystemTests.cs` — sobreposição lança; buracos em `HasTile`/`GetTile`; `GetNeighbors` ordem fixa ignorando buracos/bordas. **⚠️ v6: os testes de BFS (`GetReachable_*`) foram removidos** junto com o método.
- `GridDataTests.cs` — round-trip `GridData → GridSystem`; **hash independente da ordem dos tiles**; hash muda quando a altura muda.

---

## Como demonstrar (passo a passo no Unity)

1. Abrir/È criar uma cena vazia.
2. Menu **`GAMBIT/Grid/Create Demo Grid`** → o tabuleiro 7×7 irregular aparece (buraco em (3,3),(3,4),(4,3); pedras em (6,1),(1,5)).
3. Menu **`GAMBIT/Grid/Bake Scene To GridData`** → escolher onde salvar o `.asset` → gera o `GridData` (log com nº de tiles e hash).
4. *(Opcional, fluxo desacoplado)* num objeto vazio, adicionar `GridVisualizer`, apontar o `GridData` baked e dar **Play** → o tabuleiro é redesenhado a partir do dado.
5. **Test Runner** (Window > General > Test Runner > EditMode > Run All) → todos os testes de Grid + FSM + EventBus passando.

---

## Decisões e desvios desta sessão

- **`Tile.Occupant` adiado para o Marco 2.** Depende de `Piece` (Piece System, M2). A política de movimento (padrões fixos por peça, deslizar com caminho livre) tratará bloqueio/ocupação na validação do comando — coerente com *"Grid fornece topologia; peça/regra fornece a política"*. Quando `Piece` existir, basta adicionar `Occupant` + `IsOccupied` derivado.
- **Novo asmdef `Game.Core.Editor`** (Editor-only, referencia só `Game.Core`) para isolar o código de bake que usa `UnityEditor`, mantendo o runtime do core limpo.
- **`TileAuthoring`/`GridRuntime` no `Game.Core`**: são MonoBehaviours sem render/UI (Core pode usar MonoBehaviours; a regra é não referenciar `Game.Visual`/UI). Fronteira verificada: nenhuma referência proibida no Core.
- **`ContentHash` por XOR** dos hashes de cada tile → independente da ordem de coleta do bake, mas sensível a coordenada/altura/tipo. Já pronto para virar âncora do cabeçalho do log no Marco 5.
- **`TileType` detalhado só no mínimo** (bloqueio + cor), como prevê a arquitetura (seção 4: detalhamento completo — gatilhos de evento de mapa, altura que bloqueia por tipo — fica para o Marco 5).

## Pendências / não fazem parte do escopo do M1

- Rodar a suíte no Test Runner do Unity (ação manual do usuário).
- `Occupant` no `Tile` (M2).
- Diferença de altura como bloqueio: virá com a validação dos `MovePattern` (caminho livre) no Piece System / Command Pipeline (M2); o `MaxStepHeight` entra ali.
- Evento `GridBuiltEvent` no bus: não há bus compartilhado instanciado até o GameManager (M4); o visual lê o `GridData` direto por ora.

---

## Próximo: Marco 2 — Uma peça que anda (vertical slice)

Piece System + Command Pipeline (mínimos): `PieceDefinition`/`Piece` + `MoveActionDefinition`; pipeline com `MoveCommand` + `Submit` + validação em duas camadas + log; clicar peça → alcance destacado (`ReachableTilesComputedEvent`) → clicar tile válido → peça anda.
