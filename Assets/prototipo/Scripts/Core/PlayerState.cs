using System;

namespace Prototipo.Core
{
    /// <summary>
    /// Estado de recursos de um jogador na partida.
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerTeam Team { get; }
        public int CurrentAP { get; internal set; }
        public int MaxAP { get; internal set; }

        public PlayerState(PlayerTeam team, int maxAP)
        {
            Team = team;
            MaxAP = Math.Max(1, maxAP);
            CurrentAP = MaxAP;
        }
    }
}
