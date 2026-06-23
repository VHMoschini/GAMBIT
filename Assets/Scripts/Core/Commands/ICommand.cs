namespace Game.Core.Commands
{
    /// <summary>
    /// Unidade de mutação de estado de partida. Imutável e autossuficiente: carrega tudo que seu
    /// <see cref="Validate"/>/<see cref="Execute"/> precisam (nunca infere do contexto ambiente), de modo
    /// que o log fique legível e re-executável por si só.
    /// </summary>
    public interface ICommand
    {
        /// <summary>Converte o comando em "válido ou recusa motivada". <b>Nada muta aqui.</b></summary>
        Result Validate(GameContext context);

        /// <summary>Aplica a mutação via sistemas donos e publica eventos (fato completo) no bus.</summary>
        void Execute(GameContext context);
    }
}
