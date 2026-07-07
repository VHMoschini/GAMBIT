# Ordem de Desenvolvimento — Clone Capsule Monster Coliseum (Unity)

Companion do documento de arquitetura do core. Define a **ordem de construção presando entregáveis** — diferente da ordem de dependências do design.

---

## Princípio: antecipar o vertical slice

A ordem de dependências diz *o que precisa existir antes de quê*. Esta ordem otimiza outra coisa: cada marco produz algo **rodável e demonstrável**, mesmo que parcial.

Em vez de construir os 8 sistemas completos em ordem de dependência e só ver algo jogável no fim, **atravessamos toda a stack cedo** (Marco 2) com o mínimo de cada sistema, e depois *engrossamos* cada camada. Isso entrega:

- Algo demonstrável quase toda sessão.
- Validação da arquitetura antes de investir pesado nela.
- Marcos com escopo pequeno o suficiente para estimar e medir (time tracking).

**Sistemas parciais são deliberados.** Command Pipeline e Piece System nascem mínimos no Marco 2 e crescem nos Marcos 3–5. A arquitetura permite porque os comandos são unidades independentes — adicionar `AttackCommand` depois não mexe no `MoveCommand`; ativar stamina no Marco 4 só usa um ponto de extensão (pré-checagem) que o pipeline já tem.

---

## Marco 0 — Fundação invisível

**Sistemas:** Event System + FSM + asmdef.

Sem entregável visual, mas é a base que tudo penca. Integrar a GenericEventBus, escrever o wrapper de debug, montar a `StateMachine<TContext>` POCO (push/pop + validação de transição) e cravar os três asmdef (`Core`, `Visual`, `Core.Tests`).

> **Entregável:** suite de testes de edit mode da FSM passando (transição inválida lança, `AnyState`, push/pop preserva coerência). Demonstrável como qualidade, mesmo sem tela.

Curto e de altíssimo retorno — a FSM sustenta GameManager e HumanController depois.

---

## Marco 1 — Grid que se vê

**Sistemas:** Grid System + Tile (como dado) + bake.

Primeiro entregável visual real: o tabuleiro na tela. Grid esparso POCO, `GridCoord`, `GetNeighbors` (ordem fixa), `GetReachable` (BFS), `Tile` como dado puro, e o bake editor-time com `GridData` serializado. Ligar os tiles placeholder coloridos (Kenney) escutando o bake.

> **Entregável:** abrir a cena, ver o tabuleiro irregular montado (com buraco), e um teste provando que o BFS contorna o buraco.

---

## Marco 2 — Uma peça que anda (vertical slice)

**Sistemas (parciais):** Piece System + Command Pipeline.

O salto para "jogo". Cria parcialmente dois sistemas ao mesmo tempo:
- **Pipeline mínimo:** só `MoveCommand` + `Submit` + validação em duas camadas + log.
- **Piece mínimo:** `PieceDefinition`/`Piece` + `MoveActionDefinition`.

Sem stamina, sem turno, sem ataque.

> **Entregável:** clicar numa peça → ver o alcance destacado (evento `ReachableTilesComputed`) → clicar num tile válido → a peça anda, com a animação tocando a partir do payload do evento.

**É o vertical slice que prova a arquitetura inteira de ponta a ponta:** input → comando → core → evento → visual.

---

## Marco 3 — Duas peças brigam

**Sistemas (incremento):** Command Pipeline + Piece System.

Incrementa Pipeline e Piece: `AttackCommand`, `DamageCommand` (derivado), `CurrentHP`, evento de morte, e o registro `PieceId → Piece` no processor.

> **Entregável:** uma peça ataca outra, dano resolve, peça morre e some — com a fila de derivados funcionando (move → dano → morte na ordem certa).

---

## Marco 4 — Vira partida

**Sistemas:** Turn System + Player/Controller + stamina.

Os sistemas que faltam viram gente grande. Turn System com GameManager FSM, `CurrentPlayer`, `EndTurnCommand`, stamina como gate; Player com `CurrentStamina` + cache reativo. Dois controllers humanos alternando.

> **Entregável:** partida local jogável de verdade — dois jogadores, turnos, stamina, até a condição de vitória (rei morto / sem peças).

---

## Marco 5 — Acabamento das costuras

**Sistemas:** robustez transversal + fechar Tile System.

O que torna sólido: gate de input no HumanController (`PresentationFinishedEvent`), `CommandRejectedEvent` com feedback, golden-log de replay, setup inicial via `SummonCommand`s de sistema, e fechar o Tile System parcial (eventos de mapa = comando derivado, estruturar `TileType`).

> **Entregável:** jogo robusto, com setup completo e teste de determinismo.

---

## Resumo

| Marco | Foco | Entregável demonstrável |
|-------|------|-------------------------|
| 0 | Event + FSM + asmdef | Testes de edit mode da FSM passando |
| 1 | Grid + Tile (dado) + bake | Tabuleiro irregular na tela; BFS contorna buraco |
| 2 | Piece + Pipeline (mínimos) | Peça anda (input→comando→core→evento→visual) |
| 3 | Attack + Damage + morte | Peça ataca, dano resolve, peça morre |
| 4 | Turn + Player + stamina | Partida local jogável com vitória |
| 5 | Costuras + Tile final | Jogo robusto, setup completo, replay testável |

> **Sistemas que nascem parciais e crescem:** Command Pipeline (M2→M3→M4→M5) e Piece System (M2→M3). Deliberado: comandos são unidades independentes, e os pontos de extensão já estão no design.
