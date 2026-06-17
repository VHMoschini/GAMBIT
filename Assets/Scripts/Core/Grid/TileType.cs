using UnityEngine;

namespace Game.Core.Grid
{
    /// <summary>
    /// Tipo de terreno de um tile (molde imutável, 1 asset por terreno). Dado puro do core —
    /// não referencia render/UI. A camada visual lê <see cref="DebugColor"/> para o placeholder,
    /// mas o core não sabe o que é "pintar".
    ///
    /// Escopo do Marco 1: o mínimo para topologia e bloqueio. O detalhamento completo
    /// (gatilhos de evento de mapa, diferença de altura que bloqueia por tipo) fica para o Marco 5,
    /// conforme a arquitetura (seção 4 — "falta detalhar").
    /// </summary>
    [CreateAssetMenu(fileName = "TileType", menuName = "GAMBIT/Tile Type", order = 0)]
    public sealed class TileType : ScriptableObject
    {
        [SerializeField] private string displayName = "Tile";

        [Tooltip("Cor do placeholder usada pela camada visual. O core não renderiza nada.")]
        [SerializeField] private Color debugColor = Color.gray;

        [Tooltip("Terreno intransponível: nenhuma peça consegue parar/passar nele.")]
        [SerializeField] private bool blocksMovement;

        public string DisplayName => displayName;
        public Color DebugColor => debugColor;

        /// <summary>Terreno intransponível por regra de tipo (independe de ocupação).</summary>
        public bool BlocksMovement => blocksMovement;
    }
}
