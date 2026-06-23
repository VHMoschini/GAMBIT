using System;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Identidade estável de uma peça viva. Igualdade por valor para uso como chave e para
    /// casar o payload de eventos (<c>PieceMovedEvent</c>) com a representação visual.
    ///
    /// <para>No Marco 2 o id é atribuído por quem posiciona a peça (o bootstrap da cena). A
    /// atribuição sequencial pelo <c>CommandProcessor</c> via <c>SummonCommand</c> (âncora de replay)
    /// entra com o registro <c>PieceId → Piece</c> no Marco 3.</para>
    /// </summary>
    public readonly struct PieceId : IEquatable<PieceId>
    {
        public int Value { get; }

        public PieceId(int value) => Value = value;

        public bool Equals(PieceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PieceId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(PieceId a, PieceId b) => a.Equals(b);
        public static bool operator !=(PieceId a, PieceId b) => !a.Equals(b);

        public override string ToString() => $"Piece#{Value}";
    }
}
