using System;

namespace Game.Core.Fsm
{
    /// <summary>
    /// Lançada quando se tenta uma transição não declarada (nem direta nem via AnyState),
    /// ou para um estado não registrado. Transição inválida nunca troca silenciosamente.
    /// Herda de <see cref="InvalidOperationException"/> para conveniência de captura.
    /// </summary>
    public sealed class InvalidStateTransitionException : InvalidOperationException
    {
        public InvalidStateTransitionException(string message) : base(message)
        {
        }
    }
}
