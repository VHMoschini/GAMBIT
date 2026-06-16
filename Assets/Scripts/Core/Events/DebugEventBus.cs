using System;
using System.Collections.Generic;
using GenericEventBus;
using UnityEngine;

namespace Game.Core.Events
{
    /// <summary>
    /// Camada de debug sobre a <see cref="GenericEventBus{TBaseEvent}"/> (lib MIT, intocada).
    /// Acrescenta, sem alterar a lib:
    /// <list type="bullet">
    /// <item>log de todo <c>Raise</c> com timestamp e contagem de listeners (toggle);</item>
    /// <item>histórico circular dos últimos eventos para inspeção no editor.</item>
    /// </list>
    /// A contagem de listeners é mantida interceptando <see cref="SubscribeTo{TEvent}"/>/
    /// <see cref="UnsubscribeFrom{TEvent}"/>. O dispatch real é capturado em
    /// <see cref="RaiseImmediately{TEvent}(ref TEvent)"/>, por onde passam tanto os <c>Raise</c>
    /// imediatos quanto os que a lib enfileira e libera depois.
    /// </summary>
    public class DebugEventBus<TBaseEvent> : GenericEventBus<TBaseEvent>
    {
        /// <summary>Um evento registrado no histórico circular.</summary>
        public readonly struct EventRecord
        {
            public readonly double Timestamp;
            public readonly Type EventType;
            public readonly int ListenerCount;
            public readonly string Description;

            public EventRecord(double timestamp, Type eventType, int listenerCount, string description)
            {
                Timestamp = timestamp;
                EventType = eventType;
                ListenerCount = listenerCount;
                Description = description;
            }

            public override string ToString() =>
                $"[{Timestamp:0.000}] {EventType.Name} (listeners: {ListenerCount}) {Description}";
        }

        private readonly Dictionary<Type, int> _listenerCounts = new Dictionary<Type, int>();
        private readonly EventRecord[] _history;
        private int _historyHead;   // próxima posição de escrita
        private int _historyCount;  // quantos registros válidos (≤ capacidade)

        /// <summary>Liga/desliga o log no console. Útil para silenciar nos testes.</summary>
        public bool LogToConsole { get; set; }

        /// <summary>Capacidade do histórico circular.</summary>
        public int HistoryCapacity => _history.Length;

        /// <summary>Quantos eventos há no histórico no momento.</summary>
        public int HistoryCount => _historyCount;

        public DebugEventBus(int historyCapacity = 256, bool logToConsole = false)
        {
            if (historyCapacity < 1) historyCapacity = 1;
            _history = new EventRecord[historyCapacity];
            LogToConsole = logToConsole;
        }

        public override void SubscribeTo<TEvent>(EventHandler<TEvent> handler, float priority = 0)
        {
            base.SubscribeTo(handler, priority);
            var type = typeof(TEvent);
            _listenerCounts.TryGetValue(type, out var count);
            _listenerCounts[type] = count + 1;
        }

        public override void UnsubscribeFrom<TEvent>(EventHandler<TEvent> handler)
        {
            base.UnsubscribeFrom(handler);
            var type = typeof(TEvent);
            if (_listenerCounts.TryGetValue(type, out var count))
                _listenerCounts[type] = Mathf.Max(0, count - 1);
        }

        public override bool RaiseImmediately<TEvent>(ref TEvent @event)
        {
            Record(@event);
            return base.RaiseImmediately(ref @event);
        }

        /// <summary>Quantos listeners estão inscritos no tipo exato <typeparamref name="TEvent"/>.</summary>
        public int GetListenerCount<TEvent>() where TEvent : TBaseEvent
        {
            _listenerCounts.TryGetValue(typeof(TEvent), out var count);
            return count;
        }

        /// <summary>Cópia do histórico, do mais antigo ao mais recente.</summary>
        public IReadOnlyList<EventRecord> GetHistory()
        {
            var result = new List<EventRecord>(_historyCount);
            // O registro mais antigo está em (head - count) mod capacidade.
            var start = (_historyHead - _historyCount + _history.Length) % _history.Length;
            for (var i = 0; i < _historyCount; i++)
                result.Add(_history[(start + i) % _history.Length]);
            return result;
        }

        /// <summary>Limpa o histórico (não mexe nas inscrições).</summary>
        public void ClearHistory()
        {
            _historyHead = 0;
            _historyCount = 0;
        }

        private void Record<TEvent>(TEvent @event) where TEvent : TBaseEvent
        {
            var type = typeof(TEvent);
            _listenerCounts.TryGetValue(type, out var listenerCount);
            var timestamp = Time.realtimeSinceStartupAsDouble;
            var record = new EventRecord(timestamp, type, listenerCount, @event?.ToString() ?? "null");

            _history[_historyHead] = record;
            _historyHead = (_historyHead + 1) % _history.Length;
            if (_historyCount < _history.Length) _historyCount++;

            if (LogToConsole)
                Debug.Log($"[EventBus] {record}");
        }
    }
}
