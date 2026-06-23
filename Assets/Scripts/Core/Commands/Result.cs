namespace Game.Core.Commands
{
    /// <summary>
    /// Resultado de uma validação/submissão: <b>ok</b> ou <b>recusa motivada</b>. Devolvido ao chamador
    /// (o controller decide o que fazer); a recusa também vira <c>CommandRejectedEvent</c> no bus.
    /// </summary>
    public readonly struct Result
    {
        public bool IsValid { get; }
        public string Reason { get; }

        private Result(bool isValid, string reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        public static Result Ok() => new Result(true, null);
        public static Result Reject(string reason) => new Result(false, reason);

        public override string ToString() => IsValid ? "Ok" : $"Recusado: {Reason}";
    }
}
