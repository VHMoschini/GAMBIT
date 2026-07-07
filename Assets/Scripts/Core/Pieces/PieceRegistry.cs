using System;
using System.Collections.Generic;

namespace Game.Core.Pieces
{
    /// <summary>
    /// Índice global de peças vivas: <see cref="PieceId"/> → <see cref="Piece"/>. Os comandos carregam
    /// IDs (correto para o log/replay), mas alguém precisa resolver ID em objeto — nem o Grid (só conhece
    /// as posicionadas) nem o Player (só a mão) cobrem tudo. Esta é a porta única (arquitetura, seção 5).
    ///
    /// <para>Marco 3: a peça entra no registro quando o bootstrap a posiciona e sai quando morre. No
    /// Marco 5 a entrada passa a ser o <c>SummonCommand</c> resolvido (âncora de replay); a saída por
    /// morte já é a definitiva.</para>
    /// </summary>
    public sealed class PieceRegistry
    {
        private readonly Dictionary<PieceId, Piece> _pieces = new Dictionary<PieceId, Piece>();

        /// <summary>Quantas peças vivas estão registradas.</summary>
        public int Count => _pieces.Count;

        /// <summary>Registra uma peça viva. Lança se o id já estiver em uso (ids são únicos por partida).</summary>
        public void Register(Piece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (_pieces.ContainsKey(piece.Id))
                throw new InvalidOperationException($"Já existe uma peça registrada com id {piece.Id}.");
            _pieces.Add(piece.Id, piece);
        }

        /// <summary>Remove a peça do registro (morte). Lança se o id não estiver registrado.</summary>
        public void Unregister(PieceId id)
        {
            if (!_pieces.Remove(id))
                throw new InvalidOperationException($"Nenhuma peça registrada com id {id} para remover.");
        }

        /// <summary>Peça viva com este id, ou <c>null</c> (morta/nunca registrada).</summary>
        public Piece Get(PieceId id) => _pieces.TryGetValue(id, out var piece) ? piece : null;
    }
}
