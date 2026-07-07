using System.Collections.Generic;
using Game.Core.Pieces.Actions;
using UnityEngine;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Molde imutável de um tipo de peça (1 asset por tipo). A instância (<see cref="Piece"/>) referencia
    /// a definição, nunca a duplica.
    ///
    /// <para>Marco 2 (slice de movimento): nome, <see cref="MovePatterns"/> e <see cref="Actions"/>.
    /// Marco 3 (ataque/dano): stats de combate (<see cref="Attack"/>/<see cref="Defense"/>/<see cref="MaxHP"/>),
    /// exercitados por ataque/dano/morte. <c>IsVictoryCritical</c> (peça-chave) entra no Marco 4, quando o
    /// juiz de fim de jogo passa a lê-lo — em M3 nenhum caso o exercita (§2 de code-patterns: campo sem
    /// leitor é cortado até o caso chegar).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PieceDefinition", menuName = "GAMBIT/Piece Definition", order = 1)]
    public sealed class PieceDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Piece";

        [Header("Combate (Marco 3)")]
        [Tooltip("Poder de ataque: base do dano (dano = max(1, Attack do atacante - Defense do alvo)).")]
        [SerializeField] private int attack = 1;

        [Tooltip("Defesa: subtraída do ataque no cálculo de dano.")]
        [SerializeField] private int defense = 0;

        [Tooltip("Vida máxima; a instância começa com CurrentHP = MaxHP.")]
        [SerializeField] private int maxHP = 1;

        [Header("Movimento e ações")]
        [Tooltip("Conjunto fixo de movimentos canônicos da peça (espelhados conforme o dono).")]
        [SerializeField] private List<MovePattern> movePatterns = new List<MovePattern>();

        [Tooltip("Ações disponíveis (Move e/ou Attack).")]
        [SerializeField] private List<ActionDefinition> actions = new List<ActionDefinition>();

        public string DisplayName => displayName;
        public int Attack => attack;
        public int Defense => defense;
        public int MaxHP => maxHP;
        public IReadOnlyList<MovePattern> MovePatterns => movePatterns;
        public IReadOnlyList<ActionDefinition> Actions => actions;

        /// <summary>Cria uma definição configurada em runtime (tooling/testes). Em produção, autorada no Inspector.</summary>
        public static PieceDefinition Create(
            string displayName, IEnumerable<MovePattern> patterns, IEnumerable<ActionDefinition> actions = null,
            int attack = 1, int defense = 0, int maxHP = 1)
        {
            var def = CreateInstance<PieceDefinition>();
            def.displayName = displayName;
            def.attack = attack;
            def.defense = defense;
            def.maxHP = maxHP;
            def.movePatterns = patterns != null ? new List<MovePattern>(patterns) : new List<MovePattern>();
            def.actions = actions != null ? new List<ActionDefinition>(actions) : new List<ActionDefinition>();
            return def;
        }
    }
}
