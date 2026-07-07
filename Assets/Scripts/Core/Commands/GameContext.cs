using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;

namespace Game.Core.Commands
{
    /// <summary>
    /// Fachada passada a <c>Validate</c>/<c>Execute</c>: ponto de acesso aos sistemas <b>donos</b> do
    /// estado + o canal de eventos. Não é saco de estado — o estado mora nos sistemas (o Grid é dono
    /// dos tiles/ocupação; o <see cref="PieceRegistry"/> é o índice de peças vivas).
    ///
    /// <para>Marco 3 acrescenta o registro <see cref="GetPiece"/> (resolver id → peça) e a porta da
    /// <b>fila de derivados</b> (<see cref="EnqueueDerived"/>): um <c>Execute</c> que gera efeito (ataque →
    /// dano) enfileira o derivado aqui; o <see cref="CommandProcessor"/> é o único que drena a fila.
    /// <c>Players</c>/<c>CurrentPlayer</c> (Turn System) entram no Marco 4.</para>
    /// </summary>
    public sealed class GameContext
    {
        public GridSystem Grid { get; }
        public GameEventBus Events { get; }
        public PieceRegistry Pieces { get; }

        private readonly Queue<ICommand> _derived = new Queue<ICommand>();

        public GameContext(GridSystem grid, GameEventBus events, PieceRegistry pieces)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        }

        /// <summary>Peça viva com este id, ou <c>null</c>. Porta única de resolução id → peça.</summary>
        public Piece GetPiece(PieceId id) => Pieces.Get(id);

        /// <summary>
        /// Enfileira um comando derivado (efeito de um <c>Execute</c>: ataque → dano, lava → dano...).
        /// Não executa inline — o processor drena a fila FIFO <b>depois</b> que o <c>Execute</c> pai termina
        /// (sem recursão, eventos em ordem real de resolução — arquitetura, seção 5).
        /// </summary>
        public void EnqueueDerived(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _derived.Enqueue(command);
        }

        /// <summary>Retira o próximo derivado da fila. Uso exclusivo do <see cref="CommandProcessor"/>.</summary>
        internal bool TryDequeueDerived(out ICommand command)
        {
            if (_derived.Count > 0)
            {
                command = _derived.Dequeue();
                return true;
            }

            command = null;
            return false;
        }
    }
}
