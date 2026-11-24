# GAMBIT

Jogo tático 3D em turnos no **Unity 6** (`6000.6.10f1`, URP) inspirado em **Yu-Gi-Oh! Capsule Monster Coliseum**.

> **Usando um assistente de IA neste projeto?** Leia [`AGENTS.md`](./AGENTS.md) — ele é o guia principal de contexto, regras de arquitetura e armadilhas de build para qualquer IA (Antigravity, Cursor, Codex, Claude Code, Copilot, etc.).

---

## Estrutura do Repositório

O projeto mantém **duas trilhas independentes**:

1. **Protótipo Jogável (`Assets/prototipo/`)** — versão rápida, 100% isolada e pronta para jogar localmente ou online via Steam, usada para validar gameplay, balanceamento e visual.
   - Guia completo de regras, peças, texturas Kenney e multiplayer: [`Assets/prototipo/README.md`](./Assets/prototipo/README.md)
   - Cena: `Assets/prototipo/Scenes/Prototipo.unity`
2. **Core Definitivo (`Assets/Scripts/Core/` e `Assets/Scripts/Visual/`)** — base arquitetural do jogo completo (EventBus, FSM, Grid com bake, Command Pipeline com fila FIFO de derivados, Piece System e suíte de testes EditMode).
   - Documentação viva da arquitetura e progresso dos marcos: [`Assets/Contexts/`](./Assets/Contexts/) (`arquitetura-core-v5.md`, `code-patterns.md`, `ordem-desenvolvimento.md`, `progresso-marco-1..3.md`)
   - Cena: `Assets/Scenes/Game.unity`

---

## Como Rodar o Protótipo (1 Clique no Unity)

1. Abra o projeto no **Unity `6000.6.10f1`**.
2. No menu superior, clique em **`GAMBIT > Prototipo > Criar e Abrir Cena do Prototipo`** (ou **`Abrir Cena do Prototipo`** se a cena já existir).
3. Pressione **Play** para jogar localmente ou testar multiplayer.

### Build Windows + Multiplayer Steam (AppID 480 / Spacewar)

1. Com o cliente da **Steam** aberto nos dois PCs, gere a build pelo menu:
   **`GAMBIT > Prototipo > Build Windows + Steam (steam_appid.txt)`**
2. A pasta `Builds/PrototipoSteam/` é criada com `GAMBIT_Prototipo.exe` e `steam_appid.txt` (`480`) automaticamente ao lado do executável.
3. Um jogador cria a sala (**Criar Sala Steam**) e convida pelo Overlay da Steam ou envia o código numérico; o outro aceita o convite ou entra pelo código.

---

## Requisitos e Dependências Principais

- **Unity:** `6000.6.10f1` (URP `17.6.0`, New Input System `1.20.1`, Cinemachine `6.6.0`, uGUI/TMP `2.6.0`)
- **Steamworks.NET:** `com.rlabrecque.steamworks.net` (`#2025.165.0`, instalado via UPM no `Packages/manifest.json`)
- **Texturas de Prototipagem:** Kenney Prototype Textures (`Assets/Textures/Prototype/`)
- **Testes:** Unity Test Runner → **EditMode** (`Game.Core.Tests` e `Prototipo.Tests`)
