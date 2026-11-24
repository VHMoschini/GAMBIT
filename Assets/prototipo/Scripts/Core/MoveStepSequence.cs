using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototipo.Core
{
    /// <summary>
    /// Sequência ordenada de passos relativos à origem (na orientação canônica do Jogador 1).
    /// As casas anteriores ao último passo são etapas intermediárias do deslizamento.
    /// </summary>
    [Serializable]
    public sealed class MoveStepSequence
    {
        [SerializeField] private string label;
        [SerializeField] private List<BoardCoord> steps = new List<BoardCoord>();

        public string Label => label;
        public IReadOnlyList<BoardCoord> Steps => steps;

        public MoveStepSequence()
        {
        }

        public MoveStepSequence(string label, params BoardCoord[] steps)
        {
            this.label = label ?? string.Empty;
            this.steps = steps != null
                ? new List<BoardCoord>(steps)
                : new List<BoardCoord>();
        }
    }
}
