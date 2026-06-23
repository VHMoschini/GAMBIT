using System.Collections.Generic;
using Game.Core.Events;
using GenericEventBus;
using UnityEngine;

namespace Game.Visual.Grid
{
    /// <summary>
    /// Escuta <see cref="ReachableTilesComputedEvent"/>/<see cref="ReachableTilesClearedEvent"/> e
    /// pinta/despinta os tiles do <see cref="GridVisualizer"/>. Demonstra a fronteira: o core só publica
    /// as casas a destacar; o visual reage — o core não sabe o que é "pintado".
    /// </summary>
    public sealed class TileHighlighter : MonoBehaviour
    {
        [SerializeField] private GridVisualizer visualizer;
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.3f, 1f);

        private GameEventBus _bus;
        private readonly Dictionary<GameObject, Color> _originalColors = new Dictionary<GameObject, Color>();
        private GenericEventBus<IGameEvent>.EventHandler<ReachableTilesComputedEvent> _onComputed;
        private GenericEventBus<IGameEvent>.EventHandler<ReachableTilesClearedEvent> _onCleared;

        /// <summary>Liga este highlighter ao bus da partida. Re-chamável (re-inscreve com segurança).</summary>
        public void Bind(GameEventBus bus)
        {
            Unbind();
            _bus = bus;
            _onComputed = OnComputed;
            _onCleared = OnCleared;
            _bus.SubscribeTo(_onComputed);
            _bus.SubscribeTo(_onCleared);
        }

        private void OnComputed(ref ReachableTilesComputedEvent e)
        {
            ClearHighlights();
            if (visualizer == null || e.Tiles == null) return;

            foreach (var coord in e.Tiles)
            {
                if (!visualizer.TileObjects.TryGetValue(coord, out var tile)) continue;
                var renderer = tile.GetComponentInChildren<MeshRenderer>();
                if (renderer == null) continue;

                if (!_originalColors.ContainsKey(tile)) _originalColors[tile] = renderer.material.color;
                renderer.material.color = highlightColor;
            }
        }

        private void OnCleared(ref ReachableTilesClearedEvent e) => ClearHighlights();

        private void ClearHighlights()
        {
            foreach (var entry in _originalColors)
            {
                if (entry.Key == null) continue;
                var renderer = entry.Key.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material.color = entry.Value;
            }
            _originalColors.Clear();
        }

        private void Unbind()
        {
            if (_bus == null) return;
            if (_onComputed != null) _bus.UnsubscribeFrom(_onComputed);
            if (_onCleared != null) _bus.UnsubscribeFrom(_onCleared);
            _bus = null;
        }

        private void OnDestroy() => Unbind();
    }
}
