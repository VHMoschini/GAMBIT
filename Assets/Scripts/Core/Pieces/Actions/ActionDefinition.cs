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
        /// Constrói o comando tipado desta ação. <paramref name="target"/> é interpretado pelo subtipo
        /// (para <see cref="MoveActionDefinition"/>, a casa-destino do movimento escolhido).
        /// </summary>
        public abstract ICommand BuildCommand(Piece source, GridCoord target);
    }
}
