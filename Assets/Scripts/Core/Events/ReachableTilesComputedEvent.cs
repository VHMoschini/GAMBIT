using System.Collections.Generic;
using Game.Core.Grid;
using Game.Core.Pieces;

namespace Game.Core.Events
{
    /// <summary>
    /// Casas a destacar para o jogador: os <b>destinos dos padrões de movimento jogáveis</b> (caminho
    /// livre) da peça selecionada. A camada visual escuta e pinta; o core não sabe o que é "pintado".
    /// </summary>
    public readonly struct ReachableTilesComputedEvent : IGameEvent
    {
        public readonly PlayerId Player;
        public readonly IReadOnlyList<GridCoord> Tiles;

        public ReachableTilesComputedEvent(PlayerId player, IReadOnlyList<GridCoord> tiles)
        {
            Player = player;
            Tiles = tiles;
        }

        public override string ToString() => $"ReachableTilesComputed({Player}, {Tiles?.Count ?? 0} tiles)";
    }
}
