namespace Game.Core.Events
{
    /// <summary>
    /// Marcador base de todos os eventos de jogo. Eventos são <c>struct</c> imutáveis que
    /// carregam o <b>fato completo</b> do momento em que ocorreram (ex: <c>from/to</c>,
    /// <c>hpBefore/hpAfter</c>) — o visual nunca lê o estado atual do core para apresentar
    /// um evento passado.
    /// </summary>
    public interface IGameEvent
    {
    }
}
