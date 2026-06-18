using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Core.Common
{
    /// <summary>
    /// Fábrica de GameObjects que <b>carimba no nome a classe que os instanciou</b> — rastreabilidade no
    /// Hierarchy/Inspector e sem "referências fantasmas" sem dono explícito. Padrão de nome:
    /// <c>"NomeBase [ClasseQueInstanciou]"</c> (ex: <c>"Tile_3_2 [GridVisualizer]"</c>).
    ///
    /// <para><b>Convenção do projeto (ver <c>Assets/Contexts/code-patterns.md</c>):</b> todo instanciamento
    /// de GameObject passa por aqui. O <c>owner</c> é a instância chamadora (<c>this</c>) ou, em contexto
    /// estático, o <see cref="Type"/> da classe (<c>typeof(MinhaClasse)</c>).</para>
    /// </summary>
    public static class SceneObjectFactory
    {
        /// <summary>Instancia <paramref name="original"/>, parenteia e nomeia <c>"baseName [owner]"</c>.</summary>
        public static T Instantiate<T>(T original, string baseName, Transform parent, object owner)
            where T : Object
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            var instance = Object.Instantiate(original, parent);
            instance.name = Stamp(baseName, owner);
            return instance;
        }

        /// <summary>Cria um primitivo, parenteia e nomeia <c>"baseName [owner]"</c>.</summary>
        public static GameObject CreatePrimitive(PrimitiveType type, string baseName, Transform parent, object owner)
        {
            var go = GameObject.CreatePrimitive(type);
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.name = Stamp(baseName, owner);
            return go;
        }

        /// <summary>Cria um GameObject vazio, parenteia e nomeia <c>"baseName [owner]"</c>.</summary>
        public static GameObject Create(string baseName, Transform parent, object owner)
        {
            var go = new GameObject(Stamp(baseName, owner));
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        /// <summary>Carimba o dono no nome de um objeto já existente (use quando não controlar a criação).</summary>
        public static void StampOwner(Object obj, object owner)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.name = Stamp(obj.name, owner);
        }

        /// <summary><c>"baseName [owner]"</c>. Idempotente: não recarimba se o sufixo já existe.</summary>
        public static string Stamp(string baseName, object owner)
        {
            var suffix = $" [{ResolveOwnerName(owner)}]";
            return baseName.EndsWith(suffix, StringComparison.Ordinal) ? baseName : baseName + suffix;
        }

        private static string ResolveOwnerName(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return owner is Type type ? type.Name : owner.GetType().Name;
        }
    }
}
