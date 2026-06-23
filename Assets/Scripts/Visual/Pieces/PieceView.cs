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
    /// Representação visual de uma peça. Anima o deslocamento ao receber <see cref="PieceMovedEvent"/>,
    /// usando o <b>fato completo</b> do payload (<c>from</c>/<c>to</c>) — nunca lê o estado atual do core
    /// (que já avançou quando o evento chega). Roda atrasado em relação ao core, sem acoplamento.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private float moveDuration = 0.35f;

        private PieceId _id;
        private GameEventBus _bus;
        private Func<GridCoord, Vector3> _worldOf;
        private GenericEventBus<IGameEvent>.EventHandler<PieceMovedEvent> _onMoved;
        private Coroutine _animation;

        /// <summary>Liga a view a um id de peça e ao bus; <paramref name="worldOf"/> mapeia coordenada → posição de mundo.</summary>
        public void Bind(PieceId id, GameEventBus bus, Func<GridCoord, Vector3> worldOf)
        {
            _id = id;
            _bus = bus;
            _worldOf = worldOf;
            _onMoved = OnMoved;
            _bus.SubscribeTo(_onMoved);
        }

        private void OnMoved(ref PieceMovedEvent e)
        {
            if (!e.Piece.Equals(_id)) return;

            var from = _worldOf(e.From);
            var to = _worldOf(e.To);
            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(Animate(from, to));
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
            if (_bus != null && _onMoved != null) _bus.UnsubscribeFrom(_onMoved);
        }
    }
}
