namespace Game.Core.Pieces
{
    /// <summary>
    /// Lado do tabuleiro / dono de peças. Multiplayer <b>local</b> = dois ids fixos.
    /// A "frente" do movimento é <b>relativa ao dono</b>: a trajetória canônica de um
    /// <see cref="MovePattern"/> é espelhada conforme o lado (ver <see cref="MoveResolver.Orient"/>).
    /// </summary>
    public enum PlayerId
    {
        Player1 = 0,
        Player2 = 1,
    }
}
