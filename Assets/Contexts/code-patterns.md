# Padrões de Código — GAMBIT

> Documento vivo. Convenções transversais de implementação que **toda sessão deve seguir** ao escrever
> código novo. Complementa `arquitetura-core-v5.md` (decisões de design dos sistemas) e
> `ordem-desenvolvimento.md` (ordem dos marcos). Se um padrão novo surgir numa sessão, **registre aqui**.

---

## 1. Instanciação de GameObjects — sempre via `SceneObjectFactory`

**Regra:** nenhum código instancia GameObject "na mão" (`Instantiate`, `new GameObject`,
`GameObject.CreatePrimitive`) diretamente. Todo instanciamento passa por
`Game.Core.Common.SceneObjectFactory`, que **carimba no nome a classe que instanciou**.

**Padrão de nome:** `"NomeBase [ClasseQueInstanciou]"` — ex: `"Tile_3_2 [GridVisualizer]"`.

**Por quê:**
- **Rastreabilidade:** olhando o Hierarchy/Inspector dá pra saber *quem* criou cada objeto.
- **Sem "referências fantasmas":** objetos órfãos/sem dono explícito ficam evidentes — o nome aponta a origem.
- **Escalabilidade:** quando vários sistemas passarem a instanciar (peças, efeitos, UI de mundo), a origem
  de cada objeto continua óbvia sem caçar no código.

### API

```csharp
// instância chamadora → passa 'this'
SceneObjectFactory.Instantiate(prefab, "Tile_3_2", parent, this);
SceneObjectFactory.CreatePrimitive(PrimitiveType.Cube, "Tile_3_2", parent, this);
SceneObjectFactory.Create("PieceRoot", parent, this);     // GameObject vazio

// contexto estático (classes static, editor builders) → passa o Type
SceneObjectFactory.CreatePrimitive(PrimitiveType.Cube, "Tile_3_2", root.transform, typeof(GridDemoBuilder));

// objeto que você não criou, mas quer carimbar
SceneObjectFactory.StampOwner(existingObject, this);

// só a string (sem criar nada)
string name = SceneObjectFactory.Stamp("Tile_3_2", this);   // "Tile_3_2 [TipoDoThis]"
```

- O `owner` é a **instância** (`this`) ou o **`Type`** (`typeof(Classe)`) em contexto estático.
- `Stamp` é **idempotente**: não duplica o sufixo se já estiver presente.
- A factory já faz o `SetParent(parent, worldPositionStays: false)` — não reparenteie de novo.

### Onde já está aplicado

- `Game.Visual.Grid.GridVisualizer` — tiles de runtime.
- `Game.Core.Editor.GridDemoBuilder` — root `DemoGrid` e tiles de autoria (via `typeof`).

> **Exceção consciente:** a factory vive em `Game.Core.Common` (não referencia Visual/UI), então Core,
> Visual e Editor podem usá-la sem quebrar a fronteira de asmdef. Gizmos não contam (não instanciam objetos).

---

## 2. Sem camadas especulativas — resolva o problema que existe hoje

**Regra:** um script novo cobre **o caso real do momento**, com a dependência mais **direta e simples** que funciona. Não adicione fallback, indireção, abstração ou ponto de extensão "por precaução" / "pra quando precisar". Antecipação de problema futuro entra **quando o problema chega** — não antes.

**Por quê:**
- **Complexidade que não paga:** cada caminho a mais (fallback, "se vazio tenta outro", camada genérica) é código pra ler, testar e manter — sem usuário real agora.
- **Atrapalha entender o funcionamento real:** quem lê não distingue o fluxo que acontece do fluxo "só por garantia". O código deixa de documentar a intenção.
- **Previsão custa caro e erra:** o formato exato do problema futuro quase nunca é o que você imaginou; a abstração precoce vira a abstração errada, e remover é mais caro que adicionar quando a necessidade for concreta.

**Como decidir (na prática):**
- A dependência tem **um** dono óbvio hoje? Aponte direto pra ele. Não ofereça "ou este, ou aquele".
- Está adicionando um campo/branch que **nenhum caso atual exercita**? Corte. Adicione no commit que trouxer o caso.
- Sentiu falta real de flexibilidade? Aí sim — com o caso concreto na mão, a abstração nasce certa.

> **Caso concreto:** o `GridVisualizer` tinha `gridData` **e** um fallback `gridRuntime` (`ResolveData` escolhia um dos dois). Nenhum fluxo do M1 usava o fallback; ele só somava um caminho e embaçava "de onde vem o dado". Enxugado pra ler **só** o `GridData` (snapshot direto — ver `arquitetura-core-v5.md` §1, snapshot vs. delta). Se um dia o tabuleiro precisar vir de outra fonte em runtime, a indireção volta **naquele** momento, modelada pelo caso real.

> **Não confunde com design de costura deliberado.** Decisões de arquitetura já ponderadas e documentadas (comando autossuficiente, evento com fato completo, asmdef separando Core/Visual) **não** são especulação — são o problema de hoje resolvido com cuidado. A regra ataca o *acréscimo silencioso* de segurança/flexibilidade que ninguém pediu e nenhum caso usa.

---

## Princípios herdados da arquitetura (lembrete)

Estes não são exclusivos deste doc, mas valem como padrão de código (detalhe em `arquitetura-core-v5.md`):

- **Core fala, visual ouve.** `Game.Core` não referencia `Game.Visual`/UI — garantido por asmdef.
- **Lógica de jogo em POCO testável** (sem MonoBehaviour quando possível); MonoBehaviour fino só carrega/expõe.
- **Estado derivado nunca é armazenado em paralelo** (`IsBlocked => Type.BlocksMovement`).
- **Ordem determinística** onde replay depende (ex: offsets de vizinhança em ordem fixa; nunca iterar
  `Dictionary` em lógica sensível a ordem).
- **Eventos carregam o fato completo** (payload com `from/to`, `hpBefore/hpAfter`).

---

## Como adicionar um padrão novo

1. Descreva a **regra** (uma frase imperativa) + o **porquê**.
2. Mostre a **API/uso** com exemplo curto.
3. Liste **onde já está aplicado**.
4. Se for um helper, diga **em qual asmdef** vive e por quê (fronteira Core↔Visual).
