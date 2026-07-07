using Game.Core.Events;
using Game.Core.Pieces;

namespace Game.Core.Commands
{
    /// <summary>
    /// Aplica dano a uma peça. É um comando <b>derivado</b> (efeito de um ataque, e no Marco 5 de terreno);
    /// entra pela fila do processor e roda seu <see cref="Validate"/> normalmente — se o alvo já morreu no
    /// item anterior da cadeia, o registro não o resolve e a recusa é natural (arquitetura, seção 5).
    ///
    /// <para>Autossuficiente: carrega <see cref="Target"/> e <see cref="Amount"/> (o cálculo do dano é de
    /// quem gerou o derivado). A <b>morte é resolvida aqui</b> quando a vida chega a zero (não é comando
    /// separado): limpa ocupação, desregistra e publica <c>PieceDiedEvent</c> logo após o <c>DamagedEvent</c>.</para>
    /// </summary>
    public sealed class DamageCommand : ICommand
    {
        public PieceId Target { get; }
        public int Amount { get; }

        public DamageCommand(PieceId target, int amount)
        {
            Target = target;
            Amount = amount;
        }

        public Result Validate(GameContext context)
        {
            var target = context.GetPiece(Target);
            if (target == null) return Result.Reject($"Alvo {Target} não está vivo/registrado.");
            return Result.Ok();
        }

        public void Execute(GameContext context)
        {
            var target = context.GetPiece(Target);

            var hpBefore = target.CurrentHP;
            target.TakeDamage(Amount);
            var hpAfter = target.CurrentHP;

            // Fato completo: o dano e o par hpBefore/hpAfter viajam no payload (o core já avançou).
            context.Events.Raise(new DamagedEvent(Target, Amount, hpBefore, hpAfter));

            if (target.IsAlive) return;

            // Morte: o Grid deixa de ser dono da casa e o registro deixa de resolver o id.
            var at = target.Position;
            context.Grid.ClearOccupant(at);
            context.Pieces.Unregister(Target);
            context.Events.Raise(new PieceDiedEvent(Target, target.Owner, at));
        }

        public override string ToString() => $"Damage {Target} -{Amount}";
    }
}
