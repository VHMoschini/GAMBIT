using Game.Core.Pieces;

namespace Game.Core.Events
{
    /// <summary>Limpa o destaque de casas do jogador (seleção cancelada ou jogada concluída).</summary>
    public readonly struct ReachableTilesClearedEvent : IGameEvent
    {
        public readonly PlayerId Player;

        public ReachableTilesClearedEvent(PlayerId player) => Player = player;

        public override string ToString() => $"ReachableTilesCleared({Player})";
    }
}
