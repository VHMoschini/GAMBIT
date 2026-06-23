using Game.Core.Commands;
using Game.Core.Grid;
using UnityEngine;

namespace Game.Core.Pieces.Actions
{
    /// <summary>
    /// Ação de movimento: constrói um <see cref="MoveCommand"/> da peça para a casa-destino escolhida
    /// (um destino de <see cref="MovePattern"/> jogável, resolvido por <see cref="MoveResolver"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "MoveAction", menuName = "GAMBIT/Actions/Move", order = 0)]
    public sealed class MoveActionDefinition : ActionDefinition
    {
        public override ICommand BuildCommand(Piece source, GridCoord target)
            => new MoveCommand(source.Owner, source.Id, source.Position, target);

        /// <summary>Cria a ação em runtime (tooling/testes). Em produção, autorada como asset.</summary>
        public static MoveActionDefinition Create(string displayName = "Move")
        {
            var action = CreateInstance<MoveActionDefinition>();
            action.SetDisplayName(displayName);
            return action;
        }
    }
}
