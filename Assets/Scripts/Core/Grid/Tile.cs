using Game.Core.Pieces;

namespace Game.Core.Grid
{
    /// <summary>
    /// Célula do grid — <b>dado puro</b> (POCO), sem FSM e sem estado de apresentação
    /// (highlight é evento, ver arquitetura seção 4). Estado derivado nunca é armazenado em paralelo.
    ///
    /// <para><b>Ocupação (Marco 2):</b> o Grid é dono do que está posicionado, via <see cref="Occupant"/>.
    /// A mutação passa pelo <see cref="GridSystem.SetOccupant"/>/<see cref="GridSystem.ClearOccupant"/>
    /// (resolução do <c>MoveCommand</c>); a <i>política</i> de movimento segue fora do core, na validação.</para>
    /// </summary>
    public sealed class Tile
    {
        /// <summary>Coordenada lógica desta célula (2D — altura não entra na coordenada).</summary>
        public GridCoord Coord { get; }

        /// <summary>Altura quantizada em níveis inteiros (gerada no bake a partir de <c>position.y</c>).</summary>
        public int HeightLevel { get; }

        /// <summary>Tipo de terreno (molde imutável compartilhado).</summary>
        public TileType Type { get; }

        public Tile(GridCoord coord, int heightLevel, TileType type)
        {
            Coord = coord;
            HeightLevel = heightLevel;
            Type = type;
        }

        /// <summary>Bloqueado por tipo de terreno. DERIVADO de <see cref="TileType"/>, nunca armazenado.</summary>
        public bool IsBlocked => Type != null && Type.BlocksMovement;

        /// <summary>Peça que ocupa a célula, ou <c>null</c>. Mutável só via <see cref="GridSystem"/>.</summary>
        public Piece Occupant { get; private set; }

        /// <summary>Ocupada por uma peça. DERIVADO de <see cref="Occupant"/>, nunca armazenado em paralelo.</summary>
        public bool IsOccupied => Occupant != null;

        /// <summary>Define/limpa o ocupante. Interno: a porta pública é <see cref="GridSystem.SetOccupant"/>/<see cref="GridSystem.ClearOccupant"/>.</summary>
        internal void SetOccupant(Piece occupant) => Occupant = occupant;
    }
}
