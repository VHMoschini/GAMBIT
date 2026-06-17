namespace Game.Core.Grid
{
    /// <summary>
    /// Célula do grid — <b>dado puro</b> (POCO), sem FSM e sem estado de apresentação
    /// (highlight é evento, ver arquitetura seção 4). Estado derivado nunca é armazenado em paralelo.
    ///
    /// <para><b>Occupant chega no Marco 2 (Piece System).</b> Até lá, ocupação não existe no core;
    /// o BFS de alcance trata a política de bloqueio via o delegate <c>canStep</c> que o chamador
    /// fornece — "Grid fornece topologia; peça/regra fornece a política".</para>
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
    }
}
