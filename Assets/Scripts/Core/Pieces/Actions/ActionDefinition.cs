using Game.Core.Commands;
using Game.Core.Grid;
using UnityEngine;

namespace Game.Core.Pieces.Actions
{
    /// <summary>
    /// Molde de uma ação (ScriptableObject abstrato). Cada subtipo concreto carrega seus próprios
    /// parâmetros no Inspector e sabe construir seu <see cref="ICommand"/> tipado — polimorfismo resolve
    /// o dispatch; ação nova = nova classe + asset (arquitetura, seção 6).
    /// </summary>
    public abstract class ActionDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Action";

        public string DisplayName => displayName;

        protected void SetDisplayName(string value) => displayName = value;

        /// <summary>
        /// Constrói o comando tipado desta ação. <paramref name="target"/> é a casa-alvo, interpretada pelo
        /// subtipo (para <see cref="MoveActionDefinition"/>, o destino do movimento; para
        /// <see cref="AttackActionDefinition"/>, a casa do inimigo). <paramref name="context"/> permite
        /// resolver o alvo pelo estado (ex: o ocupante da casa → <c>PieceId</c>).
        /// </summary>
        public abstract ICommand BuildCommand(Piece source, GridCoord target, GameContext context);
    }
}
