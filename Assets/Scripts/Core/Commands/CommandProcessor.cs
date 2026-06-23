using System;
using System.Collections.Generic;
using Game.Core.Events;

namespace Game.Core.Commands
{
    /// <summary>
    /// Único ponto que aplica mutação de estado de partida. <see cref="Submit"/> roda a <b>validação em
    /// duas camadas</b> (Camada 1: pré-checagens globais no processor; Camada 2: <c>Validate</c> do
    /// comando), executa, registra no log e deixa o <c>Execute</c> publicar os eventos. Recusa devolve
    /// <see cref="Result"/> e publica <c>CommandRejectedEvent</c>.
    /// </summary>
    public sealed class CommandProcessor
    {
        private readonly GameContext _context;
        private readonly List<ICommand> _history = new List<ICommand>();

        public CommandProcessor(GameContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>Partida inteira como lista de comandos resolvidos — base de replay/debug (âncoras no Marco 5).</summary>
        public IReadOnlyList<ICommand> History => _history;

        /// <summary>
        /// Submete uma <b>jogada</b>: Camada 1 → Camada 2 → <c>Execute</c> → log. Devolve o
        /// <see cref="Result"/>; em recusa, publica <c>CommandRejectedEvent</c> e não muta nada.
        /// </summary>
        public Result Submit(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Camada 1 — pré-checagens globais (válidas para qualquer comando).
            var global = RunGlobalPreChecks(command);
            if (!global.IsValid) return RaiseRejected(command, global);

            // Camada 2 — validação específica do comando.
            var validation = command.Validate(_context);
            if (!validation.IsValid) return RaiseRejected(command, validation);

            command.Execute(_context);
            _history.Add(command);
            return Result.Ok();
        }

        /// <summary>
        /// Camada 1: invariantes globais. No Marco 2 não há turno/fase/stamina (Turn System é Marco 4);
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
