using Game.Core.Pieces;
using UnityEngine;

namespace Game.Core.Commands
{
    /// <summary>
    /// Uma peça ataca outra. Autossuficiente — carrega <see cref="Owner"/>/<see cref="Attacker"/>/
    /// <see cref="Target"/>/<see cref="Range"/>, re-executável a partir do log.
    ///
    /// <para><see cref="Validate"/>: posse do atacante, alvo é peça <b>inimiga</b> viva e dentro do
    /// <see cref="Range"/> (distância de grid — Chebyshev; linha de visão está cortada do primeiro escopo,
    /// arquitetura seção 6). <see cref="Execute"/> não muta a vida diretamente: <b>enfileira</b> um
    /// <see cref="DamageCommand"/> derivado (ataque → dano → morte resolvem na ordem real da fila).</para>
    /// </summary>
    public sealed class AttackCommand : ICommand
    {
        public PlayerId Owner { get; }
        public PieceId Attacker { get; }
        public PieceId Target { get; }
        public int Range { get; }

        public AttackCommand(PlayerId owner, PieceId attacker, PieceId target, int range)
        {
            Owner = owner;
            Attacker = attacker;
            Target = target;
            Range = range;
        }

        public Result Validate(GameContext context)
        {
            var attacker = context.GetPiece(Attacker);
            if (attacker == null) return Result.Reject($"Atacante {Attacker} não está vivo/registrado.");
            if (attacker.Owner != Owner) return Result.Reject($"Peça {Attacker} não pertence a {Owner}.");

            var target = context.GetPiece(Target);
            if (target == null) return Result.Reject($"Alvo {Target} não está vivo/registrado.");
            if (target.Owner == Owner) return Result.Reject($"Alvo {Target} é aliado — não é atacável.");

            var distance = attacker.Position.ChebyshevDistanceTo(target.Position);
            if (distance > Range)
                return Result.Reject($"Alvo {Target} fora de alcance ({distance} > {Range}).");

            return Result.Ok();
        }

        public void Execute(GameContext context)
        {
            var attacker = context.GetPiece(Attacker);
            var target = context.GetPiece(Target);

            // Dano determinístico (placeholder — confirmar contra o jogo base): ao menos 1, sempre chipa.
            var amount = Mathf.Max(1, attacker.Definition.Attack - target.Definition.Defense);

            // Derivado, não inline: entra na fila e resolve depois deste Execute (arquitetura, seção 5).
            context.EnqueueDerived(new DamageCommand(Target, amount));
        }

        public override string ToString() => $"Attack {Attacker} -> {Target} (owner {Owner}, range {Range})";
    }
}
