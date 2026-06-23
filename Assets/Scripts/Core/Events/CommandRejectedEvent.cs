namespace Game.Core.Events
{
    /// <summary>
    /// Um comando foi recusado. Recusa também é informação que o visual apresenta (shake, som, tooltip);
    /// o <c>Result</c> retornado serve o fluxo síncrono do controller, este evento serve o feedback desacoplado.
    /// </summary>
    public readonly struct CommandRejectedEvent : IGameEvent
    {
        /// <summary>Descrição do comando recusado (<c>ToString</c> do comando).</summary>
        public readonly string Command;

        /// <summary>Motivo da recusa.</summary>
        public readonly string Reason;

        public CommandRejectedEvent(string command, string reason)
        {
            Command = command;
            Reason = reason;
        }

        public override string ToString() => $"CommandRejected({Command}: {Reason})";
    }
}
