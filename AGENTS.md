# AGENTS.md — Contexto do Projeto GAMBIT

> **Fonte única de contexto para agentes de IA** (Antigravity, Codex, Cursor, Claude Code, Copilot, Gemini CLI, etc.).
> Leia este arquivo antes de propor ou editar qualquer código neste repositório.

---

## 1. Visão Geral e Estrutura Dual (LEIA PRIMEIRO)

**GAMBIT** é um jogo tático 3D em turnos no **Unity 6** inspirado em *Yu-Gi-Oh! Capsule Monster Coliseum*.

Este repositório mantém **duas trilhas de código deliberadamente isoladas**:

| Trilha | Onde vive | Objetivo | Filosofia |
| :--- | :--- | :--- | :--- |
| **1. Core Definitivo** (jogo completo futuro) | `Assets/Scripts/Core/`, `Assets/Scripts/Visual/`, `Assets/Scripts/Tests/`, `Assets/Scenes/Game.unity`, `Assets/Contexts/` | Arquitetura definitiva de longo prazo (8 sistemas, comandos, eventos, replay) | Estrita, testada por marcos, documentada em `Assets/Contexts/` |
| **2. Protótipo Jogável** (sandbox atual) | `Assets/prototipo/` (`Prototipo.Core`, `Prototipo.Visual`, `Prototipo.Editor`, `Prototipo.Tests`) | Validar gameplay, balanceamento, visual e multiplayer online rapidamente | Desacoplada, mas pragmática e ágil para iterar |

### Regras de ouro sobre isolamento
1. **Confirme a trilha antes de editar:** se o pedido do usuário não deixar explícito se é para o **Protótipo** (`Assets/prototipo/`) ou para o **Core Definitivo** (`Assets/Scripts/`), pergunte ou deixe claro onde a mudança será feita.
2. **Zero acoplamento cruzado:** nunca crie referências entre assemblies `Game.*` e `Prototipo.*`. O protótipo deve poder ser descartado ou mantido intacto sem impactar o Core definitivo.
3. **Sem edições especulativas fora do escopo:** não refatore o Core definitivo enquanto trabalha no protótipo (e vice-versa).

---

## 2. Stack, Versões e Restrições Técnicas Invioláveis

- **Unity:** `6000.6.10f1` (`ProjectSettings/ProjectVersion.txt`)
- **Render Pipeline:** URP `17.6.0` (`com.unity.render-pipelines.universal`)
- **Pacotes principais (`Packages/manifest.json`):**
  - `com.unity.inputsystem` `1.20.1`
  - `com.unity.cinemachine` `6.6.0`
  - `com.unity.ugui` `2.6.0` (TextMeshPro incluso)
  - `com.rlabrecque.steamworks.net` `#2025.165.0` (git URL no `manifest.json`)
- **Assets/Plugins no projeto:**
  - `Assets/Scripts/EventBus/` — `GenericEventBus` (usado pelo Core definitivo)
  - `Assets/Plugins/QFSW/` — `Quantum Console`
  - `Assets/Textures/Prototype/` — pacote **Kenney Prototype Textures** (78 PNGs: `Dark/`, `Green/`, `Light/`, `Orange/`, `Purple/`, `Red/`)
  - `Assets/TextMesh Pro/` — TMP Essentials importados

### Armadilhas técnicas obrigatórias (evite quebrar compilação ou build!)

1. **C# 9.0 estrito (`LangVersion 9.0`):**
   - Use sempre `namespace Meu.Namespace { ... }` em bloco.
   - **NÃO use** file-scoped namespaces (`namespace X;`), collection expressions (`[1, 2]`), primary constructors em classes nem `required`.
2. **APIs depreciadas no Unity 6000.6:**
   - Use `Object.FindAnyObjectByType<T>()` (não `FindFirstObjectByType<T>()`, que gera `CS0618`).
   - Evite `FindObjectsByType<T>(FindObjectsSortMode)` (obsoleto no 6000.6).
   - Em TextMeshPro, use `textWrappingMode = TextWrappingModes.Normal` ou `TextWrappingModes.NoWrap` (não `enableWordWrapping`).
   - Em `TextAlignmentOptions`, use `TextAlignmentOptions.Center` (não `MidlineCenter`).
3. **New Input System exclusivo (`activeInputHandler: 1`):**
   - `UnityEngine.Input` legado lança exceção em runtime. Use apenas `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`, Input Actions).
   - Todo `EventSystem` de UI **deve** usar `InputSystemUIInputModule` (nunca `StandaloneInputModule`).
   - Sempre proteja raycasts de tabuleiro 3D com `EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()`.
4. **URP em Builds Standalone (anti-crash e anti-shader-rosa):**
   - `Shader.Find("Universal Render Pipeline/Lit")` pode ser stripado em builds Standalone se nenhum material serializado referenciar o shader.
   - No URP `17.6`, `GraphicsSettings.currentRenderPipeline.defaultMaterial` vive em `UniversalRenderPipelineEditorMaterials` (Editor-only) e vem `null` em build — o que faz fallbacks caírem no shader Built-in e renderizarem tudo **rosa/magenta**.
   - **Regra:** mantenha materiais `.mat` reais referenciando URP Lit em `Resources/` ou referenciados na cena. No protótipo, carregue via `PrototypeMaterialFactory` + `Assets/prototipo/Resources/*.mat`.
5. **Fronteiras de `.asmdef` e Build Windows:**
   - **Nunca** referencie assemblies `*.Editor` (ex.: `Unity.Cinemachine.Editor`) em `.asmdef` de runtime — isso quebra Standalone Builds.
   - Código dependente de Steamworks deve usar `#if STEAMWORKS_NET && !DISABLESTEAMWORKS` (`versionDefines` configurado em `Prototipo.Visual.asmdef`), compilando sem warnings tanto com quanto sem Steamworks.
6. **Unity Editor aberto:**
   - Não dispare uma segunda instância de `Unity.exe -batchmode` contra este workspace se o Editor já estiver aberto (bloqueio de `Library/`).

---

## 3. Trilha 1 — Core Definitivo (`Assets/Scripts/` + `Assets/Contexts/`)

### Documentação obrigatória antes de alterar o Core
Leia os docs vivos em `Assets/Contexts/`:
- `Assets/Contexts/arquitetura-core-v5.md` — visão completa dos 8 sistemas e decisões de arquitetura (atualizado até v7).
- `Assets/Contexts/code-patterns.md` — convenções transversais de implementação.
- `Assets/Contexts/ordem-desenvolvimento.md` — sequência de entrega por marcos (Marcos 0 a 5).
- `Assets/Contexts/progresso-marco-1.md`, `progresso-marco-2.md`, `progresso-marco-3.md` — o que já está implementado e testado.

### Status atual do Core
- **Concluídos:** Marco 0 (EventBus + FSM + asmdefs), Marco 1 (Grid + Tile + Bake), Marco 2 (vertical slice Piece + `MoveCommand`), Marco 3 (`AttackCommand` + `DamageCommand` derivado + `PieceRegistry` + morte).
- **Próximo marco:** **Marco 4** — Turn System, Player/Controller, Stamina e condição de vitória.
- **Cena principal do Core:** `Assets/Scenes/Game.unity`.
- **Assemblies:**
  - `Game.Core` (`Assets/Scripts/Core/`, 41 scripts incluindo subpasta `Editor/` em `Game.Core.Editor`)
  - `Game.Visual` (`Assets/Scripts/Visual/`, 5 scripts)
  - `Game.Core.Tests` (`Assets/Scripts/Tests/`, 11 arquivos de teste EditMode)

### Convenções invioláveis do Core Definitivo
- **Core fala, Visual ouve:** `Game.Core` não referencia `Game.Visual` nem UI (garantido por `.asmdef`).
- **Lógica em POCO testável:** regras vivem em classes C# puras; `MonoBehaviour` é apenas ponte fina de cena/Inspector.
- **Instanciação via `SceneObjectFactory`:** nunca chame `Instantiate`, `new GameObject` ou `CreatePrimitive` direto no Core/Visual/Editor — use `Game.Core.Common.SceneObjectFactory` (carimba `"Nome [ClasseQueInstanciou]"`).
- **Sem camadas especulativas:** resolva o caso concreto atual sem criar fallbacks ou abstrações "para quando precisar".
- **Eventos com fato completo:** payloads carregam o estado antes/depois (ex.: `from/to`, `hpBefore/hpAfter`, `owner/at`).
- **Comandos autossuficientes + fila FIFO de derivados:** `AttackCommand.Execute` enfileira `DamageCommand` na fila FIFO do `CommandProcessor` (sem recursão inline).
- **Estado derivado nunca é duplicado:** use propriedades calculadas (`IsBlocked => Type.BlocksMovement`, `IsAlive => CurrentHP > 0`).
- **Determinismo:** ordem fixa em iterações relevantes para replay (nunca depender da ordem de `Dictionary`); movimento v6 usa `MovePattern` explícito sem BFS.

### Débitos técnicos conhecidos no Core (atenção!)
- `GridData.cs`: `ContentHash` usa `string.GetHashCode()`, que não é determinístico entre execuções .NET — precisará de hash estável antes do Marco 5 (replay).
- `GridBaker.cs`: ainda contém chamada obsoleta a `FindObjectsByType<T>(FindObjectsSortMode)`.
- `CameraManager.cs`: campo não utilizado (`CS0414`) e sem integração atual com `GameEventBus` ou `Game.unity`.
- `Assets/Scripts/Tests/Temp Scripts/` (`Temp.asmdef`): script temporário antigo de troca de câmera.
- `Assets/Scripts/EventBus/`: falta o arquivo `LICENSE` local da lib `GenericEventBus` (MIT).

---

## 4. Trilha 2 — Protótipo Jogável (`Assets/prototipo/`)

Documentação detalhada para humanos e expansão: `Assets/prototipo/README.md`.

### Arquitetura e arquivos-chave
- **`Prototipo.Core` (`Assets/prototipo/Scripts/Core/`):** motor C# puro sem dependência de UI/renderização:
  - `MatchEngine.cs` (regras completas, estado, validação, eventos em `MatchEvents.cs`), `BoardGrid.cs`, `BoardTile.cs`, `BoardCoord.cs`, `MovementCalculator.cs`, `MoveStepSequence.cs`, `PieceInstance.cs`, `PieceSpec.cs`, `DefaultPieceCatalog.cs`, `PlayerState.cs`, `ActionResult.cs`, `GamePhase.cs`, `PlayerTeam.cs`, `TileKind.cs`, `MatchConfigData.cs`, além dos ScriptableObjects `PieceBlueprint.cs` e `PrototypeConfig.cs`.
- **`Prototipo.Visual` (`Assets/prototipo/Scripts/Visual/`):**
  - `PrototypeGameController.cs` — orquestrador de cena, raycast New Input System, integração local + Steam online, iluminação Trilight e backdrop `#474D59`.
  - `BoardView.cs` — blocos altos estilo Kenney (`TileHeight=0.52`, `BlockedHeight=1.20`), base escura da arena e **molduras 3D vazadas** de highlight (para não sujar as cores das texturas).
  - `PieceView.cs` — cápsula fechada vs. cilindro colorido, anel de time (P1 Ciano / P2 Laranja), visor frontal, label 3D billboard, animações suaves, estado esgotado cinza (`SetActedThisTurn`) e casco neutro oculto para cápsulas inimigas fechadas no modo online.
  - `CameraRigController.cs` — câmera tática isométrica (`Q`/`E` girar, scroll zoom, auto-flip desativado automaticamente em modo online, `InputBlocked` durante modais).
  - `PrototypeHudView.cs` — HUD 100% programático em uGUI + TMP (`1920x1080` CanvasScaler): Top Bar, Toast, Setup, Inspector, Encerrar Turno, Log de Combate, Game Over e modal **Steam / Online**.
  - `PrototypeMaterialFactory.cs` & `PrototypeVisualTheme.cs` — carregamento seguro para build de materiais URP em `Assets/prototipo/Resources/` com `anisoLevel = 16` e `FilterMode.Trilinear`.
  - `SteamMatchSession.cs` & `NetCommandMessage.cs` — multiplayer Steamworks.NET.
- **`Prototipo.Editor` (`Assets/prototipo/Scripts/Editor/`):**
  - `PrototypeSceneSetup.cs` — menus Unity `GAMBIT > Prototipo > ...`.
  - `PrototypeSteamBuildProcessor.cs` — `IPostprocessBuildWithReport` que grava `steam_appid.txt` (`480`) ao lado do executável em toda build Desktop.
- **`Prototipo.Tests` (`Assets/prototipo/Scripts/Tests/MatchEngineTests.cs`):** 10 testes NUnit EditMode cobrindo setup, AP, unlock, movimento/espelhamento P2, limite de 1 ação por peça por turno, combate e vitória.

### Regras de gameplay vigentes no Protótipo
- **Tabuleiro 7×7:** zonas de spawn P1 (fileiras 0–1) e P2 (fileiras 5–6), 2 buracos simétricos `(1,3)` e `(5,3)` e 2 rochas `(2,3)` e `(4,3)`.
- **Setup:** até 4 cápsulas por jogador, Auto-Preencher, Confirmar P1 → P2 → inicia `Playing`.
- **Turnos e AP:** 6 AP por turno; ações custam AP (desbloquear cápsula, mover, atacar).
- **1 interação por peça por turno (`PieceInstance.HasActedThisTurn`):** desencapsular, mover **ou** atacar marca a peça como esgotada naquele turno — ela fica cinza (`[AGIU]`) e bloqueada até o próximo turno.
- **3 peças padrão (`Assets/prototipo/Data/Peca_*.asset`):**
  - `red` — **Guardião Vermelho** (HP 12, ATK 5, DEF 2, Alcance 1, Desbloq 3 / Mover 2 / Atacar 2 AP, 1 casa ortogonal)
  - `blue` — **Lanceiro Azul** (HP 8, ATK 4, DEF 1, Alcance 1, Desbloq 2 / Mover 1 / Atacar 2 AP, até 2 ortogonais + diagonais frontais)
  - `yellow` — **Sentinela Amarela** (HP 6, ATK 4, DEF 0, Alcance 3, Desbloq 2 / Mover 2 / Atacar 2 AP, até 2 diagonais + recuo 1)
  - Movimento do P2 é espelhado automaticamente em 180° (`-x, -z`).
- **Combate:** `Dano = Max(1, ATK - DEF)` (cápsulas fechadas têm `DEF = 0`). Vitória ao eliminar todas as peças/cápsulas inimigas.

### Materiais Kenney do Protótipo (`Assets/prototipo/Resources/`)
- `PrototipoTileNormal.mat` (`Light/texture_08.png`) e `PrototipoTileNormalDark.mat` (`Dark/texture_01.png`) — tabuleiro xadrez claro/escuro
- `PrototipoTileSpawn.mat` (`Purple/texture_05.png`) e `PrototipoTileSpawnP2.mat` (`Orange/texture_05.png`) — zonas de spawn vibrantes
- `PrototipoTileBlocked.mat` (`Dark/texture_13.png`, bloco `WALL 1×1 meter`) e `PrototipoArenaBase.mat` (`Dark/texture_04.png`)
- `PrototipoPieceBody.mat` e `PrototipoCapsuleShell.mat` (`Light/texture_02.png`), mais `PrototipoLit.mat` (fallback URP Lit)
- Customizáveis via `Assets/prototipo/Data/VisualTheme_Prototipo.asset`.

### Multiplayer Steam no Protótipo
- Usa **AppID `480` (Spacewar)** via `steam_appid.txt` na raiz do projeto (Editor) e copiado automaticamente ao lado do `.exe` na build.
- Fluxo: Host cria Lobby público (`maxMembers = 2`) → convida via Overlay da Steam (`ActivateGameOverlayInviteDialog`) ou envia o código numérico → Cliente entra (`GameLobbyJoinRequested_t`, `+connect_lobby` ou código) → handshake `client_ready` → `sync_start` → cada ação de gameplay é enviada via `SteamMatchmaking.SendLobbyChatMsg` (`NetCommandMessage` JSON) e aplicada deterministicamente no `MatchEngine` de ambos.
- `Application.runInBackground = true` é mantido ativo no modo online; cápsulas fechadas inimigas ficam visualmente ocultas e mensagens de log/toast são sanitizadas para não vazar o tipo da peça.

### Menus do Editor e Build do Protótipo
- `GAMBIT > Prototipo > Criar e Abrir Cena do Prototipo` — garante assets em `Assets/prototipo/Data/` e salva/abre `Assets/prototipo/Scenes/Prototipo.unity`.
- `GAMBIT > Prototipo > Abrir Cena do Prototipo` — abre a cena existente.
- `GAMBIT > Prototipo > Build Windows + Steam (steam_appid.txt)` — compila `Builds/PrototipoSteam/GAMBIT_Prototipo.exe` + `steam_appid.txt` (`Builds/` está no `.gitignore`).

---

## 5. Checklist Rápido de Validação para IAs

Antes de concluir qualquer alteração neste repositório:
1. **Verifique compilação sem erros nem warnings** (atenção especial a C# 9.0, APIs depreciadas do Unity 6000.6 e símbolos `#if STEAMWORKS_NET`).
2. **Rode os testes EditMode relevantes** no Unity Test Runner (ou ambiente compatível):
   - Core Definitivo: `Game.Core.Tests`
   - Protótipo: `Prototipo.Tests` (`MatchEngineTests`)
3. **Se tocou em materiais, shaders, `.asmdef` ou Steam:** garanta compatibilidade com **Standalone Windows Build** (sem `UNITY_EDITOR`, sem `Shader.Find` órfão, sem `.Editor` em runtime).
4. **Atualize a documentação quando mudar regras ou estrutura:** mantenha sincronizados este `AGENTS.md`, `Assets/Contexts/` (para o Core) e `Assets/prototipo/README.md` (para o Protótipo).
