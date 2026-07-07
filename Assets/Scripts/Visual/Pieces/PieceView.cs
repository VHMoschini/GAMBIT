using System;
using System.Collections;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;
using GenericEventBus;
using UnityEngine;

namespace Game.Visual.Pieces
{
    /// <summary>
    /// Representação visual de uma peça. Reage aos <b>fatos completos</b> do bus — anima o deslocamento
    /// (<see cref="PieceMovedEvent"/>) e some ao morrer (<see cref="PieceDiedEvent"/>) — sempre a partir do
    /// payload, nunca do estado atual do core (que já avançou quando o evento chega). Roda atrasado, sem acoplamento.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private float moveDuration = 0.35f;

        private PieceId _id;
        private GameEventBus _bus;
        private Func<GridCoord, Vector3> _worldOf;
        private GenericEventBus<IGameEvent>.EventHandler<PieceMovedEvent> _onMoved;
        private GenericEventBus<IGameEvent>.EventHandler<PieceDiedEvent> _onDied;
        private Coroutine _animation;

        /// <summary>Liga a view a um id de peça e ao bus; <paramref name="worldOf"/> mapeia coordenada → posição de mundo.</summary>
        public void Bind(PieceId id, GameEventBus bus, Func<GridCoord, Vector3> worldOf)
        {
            _id = id;
            _bus = bus;
            _worldOf = worldOf;
            _onMoved = OnMoved;
            _onDied = OnDied;
            _bus.SubscribeTo(_onMoved);
            _bus.SubscribeTo(_onDied);
        }

        private void OnMoved(ref PieceMovedEvent e)
        {
            if (!e.Piece.Equals(_id)) return;

            var from = _worldOf(e.From);
            var to = _worldOf(e.To);
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(Animate(from, to));
        }

        private void OnDied(ref PieceDiedEvent e)
        {
            if (!e.Piece.Equals(_id)) return;
            Destroy(gameObject); // OnDestroy cancela as inscrições no bus.
        }

        private IEnumerator Animate(Vector3 from, Vector3 to)
        {
            transform.position = from;
            var elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / moveDuration));
                yield return null;
            }
            transform.position = to;
            _animation = null;
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            if (_onMoved != null) _bus.UnsubscribeFrom(_onMoved);
            if (_onDied != null) _bus.UnsubscribeFrom(_onDied);
        }
    }
}
