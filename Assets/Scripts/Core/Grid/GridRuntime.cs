using UnityEngine;

namespace Game.Core.Grid
{
    /// <summary>
    /// MonoBehaviour fino que <b>só carrega</b> o <see cref="GridData"/> baked e expõe o
    /// <see cref="GridSystem"/> POCO para o resto do core (e leitura da camada visual). Nenhum
    /// <c>FindObjectsOfType</c>, nenhuma query de cena — a topologia já está serializada no asset.
    /// </summary>
    public sealed class GridRuntime : MonoBehaviour
    {
        [Tooltip("Asset baked produzido pelo GridBaker (menu GAMBIT/Grid).")]
        [SerializeField] private GridData gridData;

        /// <summary>Grid lógico construído a partir do dado baked. Disponível após <see cref="Build"/>.</summary>
        public GridSystem Grid { get; private set; }

        /// <summary>Asset baked carregado por este runtime.</summary>
        public GridData Data => gridData;

        private void Awake() => Build();

        /// <summary>(Re)constrói o <see cref="GridSystem"/> a partir do <see cref="GridData"/> atribuído.</summary>
        public void Build()
        {
            if (gridData == null)
            {
                Debug.LogError($"{nameof(GridRuntime)}: nenhum {nameof(GridData)} atribuído.", this);
                return;
            }

            Grid = gridData.CreateGridSystem();
        }
    }
}
