using System;

namespace Game.Core.Fsm
{
    /// <summary>
    /// Estado-leve: mapeia callbacks opcionais a um <see cref="IState{TContext}"/> sem
    /// precisar de uma classe dedicada. Para estados triviais (ex: marcadores de fase).
    /// Estados com lógica rica devem implementar <see cref="IState{TContext}"/> diretamente.
    /// </summary>
    public sealed class DelegateState<TContext> : IState<TContext>
    {
        private readonly Action<TContext> _onEnter;
        private readonly Action<TContext> _onExit;
        private readonly Action<TContext> _onUpdate;

        public DelegateState(
            Action<TContext> onEnter = null,
            Action<TContext> onExit = null,
            Action<TContext> onUpdate = null)
        {
            _onEnter = onEnter;
            _onExit = onExit;
            _onUpdate = onUpdate;
        }

        public void OnEnter(TContext context) => _onEnter?.Invoke(context);
        public void OnExit(TContext context) => _onExit?.Invoke(context);
        public void OnUpdate(TContext context) => _onUpdate?.Invoke(context);
    }
}
