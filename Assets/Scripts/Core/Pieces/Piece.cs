using System;
using Game.Core.Grid;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Instância viva de uma peça — só o <b>estado mutável</b>. Referencia a
    /// <see cref="PieceDefinition"/> (molde imutável), nunca a duplica.
    ///
    /// <para>Marco 2: posição. Marco 3 (ataque/dano): <see cref="CurrentHP"/> — o único stat que
    /// diverge da definição em jogo (justificado por divergir; se buffs surgirem, entra camada de
    /// modificadores sem sobrescrever a base).</para>
    /// </summary>
    public sealed class Piece
    {
        public PieceDefinition Definition { get; }
        public PieceId Id { get; }
        public PlayerId Owner { get; }
        public GridCoord Position { get; private set; }

        /// <summary>Vida atual. Inicia de <see cref="PieceDefinition.MaxHP"/> e diverge com o dano.</summary>
        public int CurrentHP { get; private set; }

        public Piece(PieceDefinition definition, PieceId id, PlayerId owner, GridCoord position)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Id = id;
            Owner = owner;
            Position = position;
            CurrentHP = definition.MaxHP;
        }

        /// <summary>Viva enquanto tem vida. DERIVADO de <see cref="CurrentHP"/>, nunca armazenado em paralelo.</summary>
        public bool IsAlive => CurrentHP > 0;

        /// <summary>Atualiza a posição lógica. Chamado pela resolução do <c>MoveCommand</c>, não em geral.</summary>
        public void MoveTo(GridCoord to) => Position = to;

        /// <summary>
        /// Debita <paramref name="amount"/> de vida (clampeado em 0). Chamado pela resolução do
        /// <c>DamageCommand</c>. Não publica evento — quem muta o estado publica o fato completo.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Dano não pode ser negativo.");
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }
    }
}
