using System;
using UnityEngine;

namespace Game.Core.Grid
{
    /// <summary>
    /// Coordenada lógica de uma célula do grid. <b>2D por princípio</b> — altura é propriedade do
    /// tile (<see cref="Tile.HeightLevel"/>), não entra na coordenada (ver arquitetura, seção 3).
    ///
    /// Valor imutável e serializável pelo Unity (campos privados via <c>SerializeField</c>, expostos
    /// como propriedades de leitura). A igualdade e o hash são por valor (X, Z), o que viabiliza usar
    /// <c>GridCoord</c> como chave de dicionário no grid esparso.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        [SerializeField] private int x;
        [SerializeField] private int z;

        public int X => x;
        public int Z => z;

        public GridCoord(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        // ---- Vizinhança: offsets em ORDEM FIXA ---------------------------------
        // Determinismo de iteração (replay, "mostrar trajeto"): nunca se itera o dicionário do grid
        // diretamente em lógica de jogo — sempre por esta ordem. Ortogonais primeiro, depois diagonais.

        /// <summary>Offsets ortogonais em ordem fixa: N, E, S, W.</summary>
        public static readonly GridCoord[] OrthogonalOffsets =
        {
            new GridCoord(0, 1),   // N
            new GridCoord(1, 0),   // E
            new GridCoord(0, -1),  // S
            new GridCoord(-1, 0),  // W
        };

        /// <summary>Offsets diagonais em ordem fixa: NE, SE, SW, NW (usados só quando o movimento é diagonal).</summary>
        public static readonly GridCoord[] DiagonalOffsets =
        {
            new GridCoord(1, 1),    // NE
            new GridCoord(1, -1),   // SE
            new GridCoord(-1, -1),  // SW
            new GridCoord(-1, 1),   // NW
        };

        // ---- Aritmética / distâncias -------------------------------------------

        public static GridCoord operator +(GridCoord a, GridCoord b) => new GridCoord(a.x + b.x, a.z + b.z);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new GridCoord(a.x - b.x, a.z - b.z);

        /// <summary>Distância de Manhattan (passos ortogonais). Útil para movimento a 4 vizinhos.</summary>
        public int ManhattanDistanceTo(GridCoord other) => Mathf.Abs(x - other.x) + Mathf.Abs(z - other.z);

        /// <summary>Distância de Chebyshev (passos com diagonal). Útil para movimento a 8 vizinhos.</summary>
        public int ChebyshevDistanceTo(GridCoord other) => Mathf.Max(Mathf.Abs(x - other.x), Mathf.Abs(z - other.z));

        // ---- Igualdade por valor -----------------------------------------------

        public bool Equals(GridCoord other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => unchecked((x * 397) ^ z);

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        public override string ToString() => $"({x}, {z})";
    }
}
