using System;
using System.Collections.Generic;

namespace Game.Core.Fsm
{
    /// <summary>
    /// FSM genérica por composição — a entidade dona "tem uma" FSM, não herda dela.
    /// POCO puro: não referencia o bus de eventos. Expõe <see cref="OnStateChanged"/>
    /// (Action C# pura) para a entidade dona repassar ao bus se quiser.
    ///
    /// Estados são identificados por <typeparamref name="TStateId"/> (tipicamente um enum) e
    /// implementados como <see cref="IState{TContext}"/> (lógica rica) ou via callbacks
    /// (<see cref="DelegateState{TContext}"/>, estados triviais).
    ///
    /// Transições são declaradas no setup; <see cref="ChangeState"/> para uma transição não
    /// declarada lança <see cref="InvalidStateTransitionException"/> — nunca troca em silêncio.
    /// Suporta <c>AnyState → X</c> (<see cref="AddAnyTransition"/>) e uma pilha de estados
    /// (<see cref="PushState"/>/<see cref="PopState"/>) para wizards de seleção.
    /// </summary>
    /// <typeparam name="TStateId">Identificador de estado (enum recomendado).</typeparam>
    /// <typeparam name="TContext">Contexto repassado aos callbacks dos estados.</typeparam>
    public sealed class StateMachine<TStateId, TContext>
    {
        private readonly TContext _context;
        private readonly IEqualityComparer<TStateId> _comparer;

        private readonly Dictionary<TStateId, IState<TContext>> _states;
        private readonly Dictionary<TStateId, HashSet<TStateId>> _transitions;
        private readonly HashSet<TStateId> _anyTransitions;

        // Topo da pilha = estado corrente. Estados abaixo do topo estão suspensos
        // (push não chama OnExit; pop não re-chama OnEnter — ver PushState/PopState).
        private readonly Stack<TStateId> _stack = new Stack<TStateId>();

        /// <summary>Dispara depois de validar e de <c>OnExit</c>, antes de <c>OnEnter</c> do destino. C# puro, sem bus.</summary>
        public event Action<TStateId, TStateId> OnStateChanged;

        public StateMachine(TContext context, IEqualityComparer<TStateId> comparer = null)
        {
            _context = context;
            _comparer = comparer ?? EqualityComparer<TStateId>.Default;
            _states = new Dictionary<TStateId, IState<TContext>>(_comparer);
            _transitions = new Dictionary<TStateId, HashSet<TStateId>>(_comparer);
            _anyTransitions = new HashSet<TStateId>(_comparer);
        }

        /// <summary>A FSM já entrou no estado inicial?</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Profundidade da pilha (1 em uso normal; &gt;1 dentro de um wizard).</summary>
        public int StackDepth => _stack.Count;

        /// <summary>Id do estado no topo da pilha. Indefinido antes de <see cref="SetInitialState"/>.</summary>
        public TStateId CurrentStateId { get; private set; }

        /// <summary>Estado no topo da pilha. <c>null</c> antes de <see cref="SetInitialState"/>.</summary>
        public IState<TContext> CurrentState { get; private set; }

        // ---- Setup -------------------------------------------------------------

        /// <summary>Registra um estado com lógica rica.</summary>
        public void AddState(TStateId id, IState<TContext> state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_states.ContainsKey(id))
                throw new InvalidOperationException($"Estado '{id}' já registrado.");
            _states.Add(id, state);
        }

        /// <summary>Registra um estado-leve a partir de callbacks opcionais.</summary>
        public void AddState(
            TStateId id,
            Action<TContext> onEnter = null,
            Action<TContext> onExit = null,
            Action<TContext> onUpdate = null)
        {
            AddState(id, new DelegateState<TContext>(onEnter, onExit, onUpdate));
        }

        /// <summary>Declara uma transição direta <paramref name="from"/> → <paramref name="to"/>.</summary>
        public void AddTransition(TStateId from, TStateId to)
        {
            RequireRegistered(from);
            RequireRegistered(to);
            if (!_transitions.TryGetValue(from, out var set))
            {
                set = new HashSet<TStateId>(_comparer);
                _transitions.Add(from, set);
            }
            set.Add(to);
        }

        /// <summary>Declara <c>AnyState → <paramref name="to"/></c>: transição válida a partir de qualquer estado.</summary>
        public void AddAnyTransition(TStateId to)
        {
            RequireRegistered(to);
            _anyTransitions.Add(to);
        }

        /// <summary>Entra no estado inicial sem exigir transição declarada. Só pode ser chamado uma vez.</summary>
        public void SetInitialState(TStateId id)
        {
            if (IsRunning)
                throw new InvalidOperationException("Estado inicial já definido; a FSM já está rodando.");
            RequireRegistered(id);

            _stack.Clear();
            _stack.Push(id);
            CurrentStateId = id;
            CurrentState = _states[id];
            IsRunning = true;
            CurrentState.OnEnter(_context);
        }

        // ---- Transições --------------------------------------------------------

        /// <summary>
        /// Substitui o estado no topo: valida → <c>OnExit</c> do atual → <see cref="OnStateChanged"/> → <c>OnEnter</c> do destino.
        /// Transição não declarada (nem via AnyState) lança <see cref="InvalidStateTransitionException"/>.
        /// </summary>
        public void ChangeState(TStateId to)
        {
            RequireRunning();
            RequireTransitionAllowed(to);

            var from = CurrentStateId;
            CurrentState.OnExit(_context);

            _stack.Pop();
            _stack.Push(to);
            CurrentStateId = to;
            CurrentState = _states[to];

            OnStateChanged?.Invoke(from, to);
            CurrentState.OnEnter(_context);
        }

        /// <summary>
        /// Empilha um novo estado (avança o wizard). O estado atual fica <b>suspenso</b> (sem <c>OnExit</c>);
        /// o destino recebe <c>OnEnter</c>. Exige transição declarada do atual para o destino.
        /// </summary>
        public void PushState(TStateId to)
        {
            RequireRunning();
            RequireTransitionAllowed(to);

            var from = CurrentStateId;
            _stack.Push(to);
            CurrentStateId = to;
            CurrentState = _states[to];

            OnStateChanged?.Invoke(from, to);
            CurrentState.OnEnter(_context);
        }

        /// <summary>
        /// Desempilha o estado do topo (volta/cancela um passo do wizard): <c>OnExit</c> do topo, e o estado
        /// revelado volta a ser corrente <b>sem re-<c>OnEnter</c></b> (estava apenas suspenso). Não desempilha o estado inicial.
        /// </summary>
        public void PopState()
        {
            RequireRunning();
            if (_stack.Count <= 1)
                throw new InvalidOperationException("Não é possível desempilhar o estado inicial (pilha tem 1 elemento).");

            var popped = CurrentStateId;
            CurrentState.OnExit(_context);

            _stack.Pop();
            CurrentStateId = _stack.Peek();
            CurrentState = _states[CurrentStateId];

            OnStateChanged?.Invoke(popped, CurrentStateId);
        }

        /// <summary>Repassa o tick ao estado corrente.</summary>
        public void Tick()
        {
            RequireRunning();
            CurrentState.OnUpdate(_context);
        }

        // ---- Internos ----------------------------------------------------------

        private bool IsTransitionAllowed(TStateId to)
        {
            if (_anyTransitions.Contains(to)) return true;
            return _transitions.TryGetValue(CurrentStateId, out var set) && set.Contains(to);
        }

        private void RequireTransitionAllowed(TStateId to)
        {
            RequireRegistered(to);
            if (!IsTransitionAllowed(to))
                throw new InvalidStateTransitionException(
                    $"Transição inválida: '{CurrentStateId}' → '{to}' não foi declarada (nem via AnyState).");
        }

        private void RequireRegistered(TStateId id)
        {
            if (!_states.ContainsKey(id))
                throw new InvalidStateTransitionException($"Estado '{id}' não registrado.");
        }

        private void RequireRunning()
        {
            if (!IsRunning)
                throw new InvalidOperationException("A FSM não foi iniciada; chame SetInitialState primeiro.");
        }
    }
}
