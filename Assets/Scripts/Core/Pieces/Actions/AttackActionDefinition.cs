using Game.Core.Commands;
using Game.Core.Grid;
using UnityEngine;

namespace Game.Core.Pieces.Actions
{
    /// <summary>
    /// Ação de ataque: constrói um <see cref="AttackCommand"/> contra a peça que ocupa a casa-alvo. O
    /// alcance é por <b>distância de grid</b> (Chebyshev); linha de visão está fora do primeiro escopo
    /// (arquitetura, seção 6). Ataque novo com regra diferente = nova classe + asset.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackAction", menuName = "GAMBIT/Actions/Attack", order = 1)]
    public sealed class AttackActionDefinition : ActionDefinition
    {
        [Tooltip("Alcance do ataque em casas (distância de Chebyshev: 1 = adjacente, inclui diagonal).")]
        [SerializeField] private int range = 1;

        public int Range => range;

        public override ICommand BuildCommand(Piece source, GridCoord target, GameContext context)
        {
            // O alvo é a peça na casa clicada. Sem ocupante → id inválido que o Validate recusa
            // (o Targeting guia a UX; o Validate é a autoridade — arquitetura, seção 6).
            var occupant = context.Grid.GetTile(target)?.Occupant;
            var targetId = occupant?.Id ?? default;
            return new AttackCommand(source.Owner, source.Id, targetId, range);
        }

        /// <summary>Cria a ação em runtime (tooling/testes). Em produção, autorada como asset.</summary>
        public static AttackActionDefinition Create(int range = 1, string displayName = "Attack")
        {
            var action = CreateInstance<AttackActionDefinition>();
            action.SetDisplayName(displayName);
            action.range = range;
            return action;
        }
    }
}
