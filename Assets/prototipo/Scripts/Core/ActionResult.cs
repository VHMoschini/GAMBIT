namespace Prototipo.Core
{
    /// <summary>
    /// Resultado padronizado de uma ação executada no <see cref="MatchEngine"/>.
    /// </summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Message { get; }

        private ActionResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public static ActionResult Ok(string message = null)
        {
            return new ActionResult(true, message ?? string.Empty);
        }

        public static ActionResult Fail(string message)
        {
            return new ActionResult(false, string.IsNullOrEmpty(message) ? "Ação inválida." : message);
        }
    }
}
