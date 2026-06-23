using System;
using System.Collections.Generic;
using Game.Core.Grid;
using UnityEngine;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Um movimento canônico e fixo de uma peça (v6 — substitui alcance por BFS). A trajetória
    /// (<see cref="Steps"/>) lista as casas atravessadas <b>em ordem</b>, relativas à origem e na
    /// <b>orientação canônica</b> (a do <see cref="PlayerId.Player1"/>); o destino é o último passo.
    ///
    /// <para>É a trajetória que viabiliza a regra de <b>deslizar</b>: o padrão só é jogável se
    /// <b>todas</b> as casas listadas existem e estão livres (ver <see cref="MoveResolver"/>). A
    /// trajetória é espelhada conforme o dono em tempo de avaliação — uma definição serve aos dois lados.</para>
    /// </summary>
    [Serializable]
    public sealed class MovePattern
    {
        [Tooltip("Rótulo opcional para UI (ex: '2 pra frente').")]
        [SerializeField] private string label;

        [Tooltip("Casas atravessadas, em ordem, relativas à origem (ex: '2 pra frente' = (0,1),(0,2)).")]
        [SerializeField] private List<GridCoord> steps = new List<GridCoord>();

        public string Label => label;
        public IReadOnlyList<GridCoord> Steps => steps;

        public MovePattern() { }

        public MovePattern(string label, params GridCoord[] steps)
        {
            this.label = label;
            this.steps = new List<GridCoord>(steps ?? Array.Empty<GridCoord>());
        }
    }
}
