using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pieces;

namespace Game.Core.Commands
{
    /// <summary>
    /// Único ponto que aplica mutação de estado de partida. <see cref="Submit"/> roda a <b>validação em
    /// duas camadas</b> (Camada 1: pré-checagens globais no processor; Camada 2: <c>Validate</c> do
    /// comando), executa, registra no log e deixa o <c>Execute</c> publicar os eventos. Recusa devolve
    /// <see cref="Result"/> e publica <c>CommandRejectedEvent</c>.
    ///
    /// <para>Marco 3: após resolver a jogada, drena a <b>fila de derivados</b> (ataque → dano → morte) em
    /// FIFO iterativo (nunca recursão); os derivados pulam a Camada 1 mas mantêm o <c>Validate</c>. Mantém
    /// também o <b>registro <see cref="PieceId"/> → <see cref="Piece"/></b> via <see cref="Register"/>
    /// (alimentado hoje pelo bootstrap; pelo <c>SummonCommand</c> no Marco 5, limpo por morte).</para>
    /// </summary>
    public sealed class CommandProcessor
    {
        private readonly GameContext _context;
        private readonly List<ICommand> _history = new List<ICommand>();

        public CommandProcessor(GameContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>Partida inteira como lista de comandos resolvidos (derivados inclusos) — base de replay/debug.</summary>
        public IReadOnlyList<ICommand> History => _history;

        /// <summary>
        /// Registra uma peça viva no registro do pipeline. Porta única de entrada (bootstrap no Marco 3;
        /// <c>SummonCommand</c> no Marco 5). A saída por morte é feita pela resolução do <c>DamageCommand</c>.
        /// </summary>
        public void Register(Piece piece) => _context.Pieces.Register(piece);

        /// <summary>
        /// Submete uma <b>jogada</b>: Camada 1 → Camada 2 → <c>Execute</c> → log, e então drena a fila de
        /// derivados. Devolve o <see cref="Result"/> da jogada; em recusa, publica <c>CommandRejectedEvent</c>.
        /// </summary>
        public Result Submit(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Camada 1 — pré-checagens globais (válidas para qualquer comando).
            var global = RunGlobalPreChecks(command);
            if (!global.IsValid) return RaiseRejected(command, global);

            var result = Resolve(command);
            DrainDerived();
            return result;
        }

        /// <summary>
        /// Camada 2 + execução + log. Compartilhado pela jogada e pelos derivados (que pulam a Camada 1).
        /// Recusa publica <c>CommandRejectedEvent</c> e não muta nada.
        /// </summary>
        private Result Resolve(ICommand command)
        {
            var validation = command.Validate(_context);
            if (!validation.IsValid) return RaiseRejected(command, validation);

            command.Execute(_context);
            _history.Add(command);
            return Result.Ok();
        }

        /// <summary>
        /// Drena a fila de derivados em FIFO iterativo (um derivado pode gerar outro; o laço cobre a cadeia
        /// sem recursão). Vitória avaliada entre itens entra no Marco 4 (juiz de fim de jogo), como a
        /// Camada 1 de turno entrará; hoje a fila só resolve efeitos.
        /// </summary>
        private void DrainDerived()
        {
            while (_context.TryDequeueDerived(out var derived))
                Resolve(derived);
        }

        /// <summary>
        /// Camada 1: invariantes globais. No Marco 3 ainda não há turno/fase/stamina (Turn System é Marco 4);
        /// este é o gancho onde esses gates entram <b>sem mexer no <see cref="Submit"/></b>.
        /// </summary>
        private Result RunGlobalPreChecks(ICommand command) => Result.Ok();

        private Result RaiseRejected(ICommand command, Result rejection)
        {
            _context.Events.Raise(new CommandRejectedEvent(command.ToString(), rejection.Reason));
            return rejection;
        }
    }
}
