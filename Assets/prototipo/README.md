# Protótipo Jogável — Capsule Monster Coliseum (`Assets/prototipo/`)

Protótipo tático completo em turnos inspirado em **Yu-Gi-Oh! Capsule Monster Coliseum**, totalmente isolado em `Assets/prototipo/` com arquitetura desacoplada (`Core` → `Visual`/`HUD` → `Editor`).

---

## Como Abrir e Jogar (1 Clique)

1. No Unity Editor, abra o menu superior:
   **`GAMBIT > Prototipo > Criar e Abrir Cena do Prototipo`**
2. O setup cria automaticamente os ScriptableObjects em `Assets/prototipo/Data/` (`Peca_Vermelha`, `Peca_Azul`, `Peca_Amarela`, `Config_Prototipo`) e salva/abre a cena `Assets/prototipo/Scenes/Prototipo.unity`.
3. Pressione **Play** para jogar (nas próximas vezes você também pode usar **`GAMBIT > Prototipo > Abrir Cena do Prototipo`**).

---

## Regras do Protótipo

### 1. Fase de Posicionamento (`Setup`)
- Cada jogador escolhe até **4 cápsulas** do catálogo (Vermelha, Azul ou Amarela) e posiciona na sua **zona de spawn** destacada (2 primeiras fileiras de cada lado).
- **Botão esquerdo** na zona livre: posiciona a cápsula selecionada.
- **Botão direito** (ou clicar na própria cápsula já posicionada): remove para reposicionar.
- Use **Auto-Preencher** para preencher rapidamente os espaços restantes e **Confirmar Posicionamento** (exige ao menos 1 cápsula) para passar do **Jogador 1** para o **Jogador 2** e iniciar a batalha.

### 2. Fase de Batalha (`Playing`)
- Cada jogador começa o turno com **6 Pontos de Ação (AP)**.
- **1 ação por peça por turno:** cada peça/cápsula pode interagir apenas **uma vez por turno** — ou **desencapsular**, ou **mover**, ou **atacar**. Depois de agir, a peça fica **cinza** (`[AGIU]`) e não pode mais ser usada até o próximo turno.
- **Cápsula Fechada → Peça Ativa:** todas as peças iniciam como cápsulas fechadas (sem poder mover ou atacar e com `DEF = 0`). Clique na sua cápsula e use **Desbloquear Cápsula** (ou clique novamente nela) para revelar o cilindro colorido correspondente pagando o custo de AP.
- **Mover e Atacar:** com uma peça ativa selecionada:
  - Casas em **Ciano** indicam movimentos válidos.
  - Casas em **Vermelho** indicam alvos inimigos ao alcance.
  - O **Jogador 2** tem seus padrões de movimento espelhados automaticamente em **180°** (`-x, -z`).
- **Combate:** `Dano = Max(1, ATK do atacante - DEF do alvo)` (cápsulas fechadas recebem dano sem redução de DEF).
- **Vitória:** vence quem eliminar todas as peças/cápsulas do oponente.

### Controles de Câmera
- **`Q` / `E`**: gira a câmera manualmente.
- **Scroll do mouse**: zoom suave.
- **Botão `Câmera Auto: ON/OFF`** no topo: ativa/desativa a rotação automática de 180° ao trocar de turno.

---

## Multiplayer Online via Steam (AppID 480 / Spacewar)

O projeto já vem configurado com **Steamworks.NET** (`com.rlabrecque.steamworks.net`) e `steam_appid.txt` (`480`).

### Como gerar a Build e jogar com seu colega
1. No Unity Editor, clique em **`GAMBIT > Prototipo > Build Windows + Steam (steam_appid.txt)`**.
   * Isso gera a pasta `Builds/PrototipoSteam/` contendo `GAMBIT_Prototipo.exe` e o `steam_appid.txt` (`480`) automaticamente ao lado do executável.
2. Envie a pasta `PrototipoSteam` (ou cada um gera a build no próprio PC pelo mesmo repositório).
3. **(Opcional) Adicionar na Steam como Jogo Não-Steam:** na Steam, vá em **Jogos > Adicionar um jogo não Steam à minha biblioteca...** e selecione `GAMBIT_Prototipo.exe` (também funciona executando o `.exe` diretamente, desde que o cliente da Steam esteja aberto e logado nos dois PCs).
4. **Conectar a partida:**
   - **Jogador 1 (Host):** no menu inicial, clique em **Criar Sala Steam (Você = Jogador 1)** → clique em **Convidar Amigo (Steam)** (abre o overlay de amigos) **ou** clique em **Copiar Código** e mande pelo Discord/WhatsApp.
   - **Jogador 2 (Cliente):** aceite o convite no chat da Steam **ou** cole o código numérico da sala e clique em **Entrar (P2)**.
   - A partida inicia sincronizada automaticamente: cada jogador vê a câmera pelo seu lado do tabuleiro, só age no próprio turno e **as cápsulas fechadas do oponente ficam ocultas (cor neutra)** até serem desencapsuladas!

---

## Tabela das 3 Peças Padrão

| Peça | Papel | HP | ATK | DEF | Desbloq | Mover | Atacar | Alcance | Padrão de Movimento |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Guardião Vermelho** | Tanque corpo-a-corpo | 12 | 5 | 2 | 3 AP | 2 AP | 2 AP | 1 | 1 casa ortogonal (N/S/L/O) |
| **Lanceiro Azul** | Flanqueador ágil | 8 | 4 | 1 | 2 AP | 1 AP | 2 AP | 1 | Até 2 casas ortogonais (deslizando) + diagonais frontais `(1,1)` e `(-1,1)` |
| **Sentinela Amarela** | Atirador tático | 6 | 4 | 0 | 2 AP | 2 AP | 2 AP | 3 | Até 2 casas nas 4 diagonais (deslizando) + recuo 1 casa `(0,-1)` |

---

## Como Expandir Facilmente

- **Adicionar uma nova peça (ex.: 4ª peça Verde/Roxa):**
  1. No Project window: **`Create > GAMBIT > Prototipo > Piece Blueprint`**.
  2. Configure `Id`, `DisplayName`, `PieceColor`, `CylinderScale`, status (`HP`/`ATK`/`DEF`), custos de AP e `MovePatterns` (sequência de passos relativos ao Jogador 1).
  3. Adicione o novo asset à lista `Piece Blueprints` de `Assets/prototipo/Data/Config_Prototipo.asset` — tanto o motor quanto os botões do painel de Setup se adaptam automaticamente!
- **Ajustar arena e economia de AP:**
  - Edite `Board Width`, `Board Height`, `Capsules Per Player` e `Action Points Per Turn` diretamente no asset `Config_Prototipo.asset`.
- **Personalizar texturas (Kenney Prototype Textures):**
  - Os materiais URP padrão em `Assets/prototipo/Resources/` já usam as texturas Kenney de `Assets/Textures/Prototype/` (`Light/texture_08` e `Dark/texture_01` no grid xadrez, `Purple/texture_05` e `Orange/texture_05` nos spawns P1/P2, `Dark/texture_13` nos blocos de obstáculo, `Dark/texture_04` na base da arena e `Light/texture_02` nos cilindros e cápsulas).
  - Você também pode trocar qualquer textura, tiling ou acabamento (smoothness) pelo ScriptableObject `VisualTheme_Prototipo.asset` em `Assets/prototipo/Data/` (ou criar novos via `Create > GAMBIT > Prototipo > Visual Theme`) e arrastá-lo para o campo `Visual Theme` do `PrototipoController`.
- **Reagir a novos eventos (áudio, VFX, IA):**
  - Inscreva novos listeners diretamente nos eventos públicos de `MatchEngine` (`OnCapsuleUnlocked`, `OnPieceMoved`, `OnPieceAttacked`, `OnTurnChanged`, `OnPhaseChanged`, `OnGameOver`, `OnLogMessage`).
