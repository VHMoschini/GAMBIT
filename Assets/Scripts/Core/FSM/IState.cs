namespace Game.Core.Fsm
{
    /// <summary>
    /// Um estado da <see cref="StateMachine{TStateId,TContext}"/>.
    /// O estado recebe o contexto da entidade dona em cada callback — a FSM é POCO
    /// e não conhece o bus nem a entidade; quem precisar de estado lê do contexto.
    /// </summary>
    /// <typeparam name="TContext">Tipo do contexto compartilhado (a entidade dona, um struct de dados, etc.).</typeparam>
    public interface IState<in TContext>
    {
        /// <summary>Chamado quando a FSM entra neste estado.</summary>
        void OnEnter(TContext context);

        /// <summary>Chamado quando a FSM sai deste estado.</summary>
        void OnExit(TContext context);

        /// <summary>Chamado a cada <see cref="StateMachine{TStateId,TContext}.Tick"/> enquanto este estado está no topo.</summary>
        void OnUpdate(TContext context);
    }
}
