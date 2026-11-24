using System;

namespace Prototipo.Core
{
    /// <summary>
    /// Instância em partida de uma cápsula/monstro pertencente a um jogador.
    /// </summary>
    public sealed class PieceInstance
    {
        public int Id { get; }
        public PieceSpec Spec { get; }
        public PlayerTeam Owner { get; }
        public BoardCoord Position { get; internal set; }
        public int CurrentHP { get; internal set; }
        public bool IsUnlocked { get; internal set; }
        public bool HasActedThisTurn { get; internal set; }
        public bool IsAlive => CurrentHP > 0;

        public PieceInstance(int id, PieceSpec spec, PlayerTeam owner, BoardCoord position)
        {
            Id = id;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Owner = owner;
            Position = position;
            CurrentHP = spec.MaxHP;
            IsUnlocked = false;
            HasActedThisTurn = false;
        }

        internal void Unlock()
        {
            IsUnlocked = true;
        }

        internal void MarkActedThisTurn()
        {
            HasActedThisTurn = true;
        }

        internal void ResetTurnAction()
        {
            HasActedThisTurn = false;
        }

        internal void MoveTo(BoardCoord destination)
        {
            Position = destination;
        }

        internal void ApplyDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentHP = Math.Max(0, CurrentHP - amount);
        }
    }
}
