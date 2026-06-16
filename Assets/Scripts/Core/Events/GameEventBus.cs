namespace Game.Core.Events
{
    /// <summary>
    /// Bus concreto do jogo: <see cref="DebugEventBus{TBaseEvent}"/> restrito a <see cref="IGameEvent"/>.
    /// Único canal de eventos core → visual. Eventos carregam o fato completo (ver <see cref="IGameEvent"/>).
    /// </summary>
    public sealed class GameEventBus : DebugEventBus<IGameEvent>
    {
        public GameEventBus(int historyCapacity = 256, bool logToConsole = false)
            : base(historyCapacity, logToConsole)
        {
        }
    }
}
