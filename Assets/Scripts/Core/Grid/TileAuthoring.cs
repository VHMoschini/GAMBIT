using UnityEngine;

namespace Game.Core.Grid
{
    /// <summary>
    /// Marcador de autoria colocado em cada tile na cena. O artista posiciona o objeto; o bake
    /// (editor) lê <see cref="Transform.position"/> para inferir a <see cref="GridCoord"/> e quantizar
    /// a altura, e lê <see cref="Type"/> como terreno. Não tem lógica de runtime — é só dado de cena
    /// consumido em editor-time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TileAuthoring : MonoBehaviour
    {
        [Tooltip("Tipo de terreno deste tile. Se vazio, o bake usa o tipo padrão informado no baker.")]
        [SerializeField] private TileType type;

        public TileType Type => type;

        public void SetType(TileType value) => type = value;
    }
}
