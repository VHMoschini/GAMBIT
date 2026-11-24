using System;
using UnityEngine;

namespace Prototipo.Core
{
    /// <summary>
    /// Coordenada inteira imutável no tabuleiro 2D (eixos X e Z).
    /// </summary>
    [Serializable]
    public struct BoardCoord : IEquatable<BoardCoord>
    {
        [SerializeField] private int x;
        [SerializeField] private int z;

        public int X => x;
        public int Z => z;

        public BoardCoord(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public int ManhattanDistanceTo(BoardCoord other)
        {
            return Math.Abs(x - other.x) + Math.Abs(z - other.z);
        }

        public int ChebyshevDistanceTo(BoardCoord other)
        {
            return Math.Max(Math.Abs(x - other.x), Math.Abs(z - other.z));
        }

        public static BoardCoord operator +(BoardCoord a, BoardCoord b)
        {
            return new BoardCoord(a.x + b.x, a.z + b.z);
        }

        public static BoardCoord operator -(BoardCoord a, BoardCoord b)
        {
            return new BoardCoord(a.x - b.x, a.z - b.z);
        }

        public static bool operator ==(BoardCoord a, BoardCoord b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BoardCoord a, BoardCoord b)
        {
            return !a.Equals(b);
        }

        public bool Equals(BoardCoord other)
        {
            return x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is BoardCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ z;
            }
        }

        public override string ToString()
        {
            return $"({x}, {z})";
        }
    }
}
