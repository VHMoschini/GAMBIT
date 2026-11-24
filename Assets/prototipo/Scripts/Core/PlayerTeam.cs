namespace Prototipo.Core
{
    /// <summary>
    /// Identifica os dois jogadores da partida.
    /// </summary>
    public enum PlayerTeam
    {
        Player1 = 0,
        Player2 = 1
    }

    /// <summary>
    /// Extensões utilitárias para <see cref="PlayerTeam"/>.
    /// </summary>
    public static class PlayerTeamExtensions
    {
        public static PlayerTeam Opponent(this PlayerTeam team)
        {
            return team == PlayerTeam.Player1 ? PlayerTeam.Player2 : PlayerTeam.Player1;
        }

        public static string ToDisplayName(this PlayerTeam team)
        {
            return team == PlayerTeam.Player1 ? "Jogador 1" : "Jogador 2";
        }
    }
}
