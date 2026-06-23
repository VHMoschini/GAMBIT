using System;
using Game.Core.Grid;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Instância viva de uma peça — só o <b>estado mutável</b>. Referencia a
    /// <see cref="PieceDefinition"/> (molde imutável), nunca a duplica.
    ///
    /// <para>No Marco 2 (slice de movimento) o estado é apenas posição. <c>CurrentHP</c> e demais
    /// stats entram no Marco 3 (ataque/dano), quando passam a divergir da definição.</para>
    /// </summary>
    public sealed class Piece
    {
        public PieceDefinition Definition { get; }
        public PieceId Id { get; }
        public PlayerId Owner { get; }
        public GridCoord Position { get; private set; }

        public Piece(PieceDefinition definition, PieceId id, PlayerId owner, GridCoord position)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Id = id;
            Owner = owner;
            Position = position;
        }

        /// <summary>Atualiza a posição lógica. Chamado pela resolução do <c>MoveCommand</c>, não em geral.</summary>
        public void MoveTo(GridCoord to) => Position = to;
    }
}
