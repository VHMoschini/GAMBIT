using System;
using Game.Core.Events;
using Game.Core.Grid;

namespace Game.Core.Commands
{
    /// <summary>
    /// Fachada passada a <c>Validate</c>/<c>Execute</c>: ponto de acesso aos sistemas <b>donos</b> do
    /// estado + o canal de eventos. Não é saco de estado — o estado mora nos sistemas (o Grid é dono
    /// dos tiles e da ocupação).
    ///
    /// <para>Escopo do Marco 2: <see cref="Grid"/> + <see cref="Events"/>. <c>Players</c>/<c>CurrentPlayer</c>
    /// (Turn System, Marco 4) e o registro <c>GetPiece(PieceId)</c> (Marco 3) entram quando há caso para eles.</para>
    /// </summary>
    public sealed class GameContext
    {
        public GridSystem Grid { get; }
        public GameEventBus Events { get; }

        public GameContext(GridSystem grid, GameEventBus events)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }
    }
}
