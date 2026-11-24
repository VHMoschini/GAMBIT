namespace Prototipo.Core
{
    /// <summary>
    /// Representa uma casa individual do tabuleiro e seu eventual ocupante.
    /// </summary>
    public sealed class BoardTile
    {
        public BoardCoord Coord { get; }
        public TileKind Kind { get; }
        public PieceInstance Occupant { get; internal set; }

        public bool IsOccupied => Occupant != null;
        public bool IsWalkable => Kind != TileKind.Blocked && Kind != TileKind.Hole;

        public BoardTile(BoardCoord coord, TileKind kind)
        {
            Coord = coord;
            Kind = kind;
        }
    }
}
