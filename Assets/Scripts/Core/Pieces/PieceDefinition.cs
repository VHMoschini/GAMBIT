using System.Collections.Generic;
using Game.Core.Pieces.Actions;
using UnityEngine;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Molde imutável de um tipo de peça (1 asset por tipo). A instância (<see cref="Piece"/>) referencia
    /// a definição, nunca a duplica.
    ///
    /// <para>Escopo do Marco 2 (slice de movimento): nome, <see cref="MovePatterns"/> e <see cref="Actions"/>.
    /// Stats de combate (Attack/Defense/MaxHP) e <c>IsVictoryCritical</c> entram no Marco 3, quando há
    /// ataque/dano/morte para exercitá-los.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PieceDefinition", menuName = "GAMBIT/Piece Definition", order = 1)]
    public sealed class PieceDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Piece";

        [Tooltip("Conjunto fixo de movimentos canônicos da peça (espelhados conforme o dono).")]
        [SerializeField] private List<MovePattern> movePatterns = new List<MovePattern>();

        [Tooltip("Ações disponíveis (no Marco 2, ao menos uma MoveActionDefinition).")]
        [SerializeField] private List<ActionDefinition> actions = new List<ActionDefinition>();

        public string DisplayName => displayName;
        public IReadOnlyList<MovePattern> MovePatterns => movePatterns;
        public IReadOnlyList<ActionDefinition> Actions => actions;

        /// <summary>Cria uma definição configurada em runtime (tooling/testes). Em produção, autorada no Inspector.</summary>
        public static PieceDefinition Create(
            string displayName, IEnumerable<MovePattern> patterns, IEnumerable<ActionDefinition> actions = null)
        {
            var def = CreateInstance<PieceDefinition>();
            def.displayName = displayName;
            def.movePatterns = patterns != null ? new List<MovePattern>(patterns) : new List<MovePattern>();
            def.actions = actions != null ? new List<ActionDefinition>(actions) : new List<ActionDefinition>();
            return def;
        }
    }
}
