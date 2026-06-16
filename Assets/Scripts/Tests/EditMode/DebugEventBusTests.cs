using GenericEventBus;
using Game.Core.Events;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DebugEventBusTests
    {
        private struct PingEvent : IGameEvent
        {
            public int Value;
            public override string ToString() => $"Ping({Value})";
        }

        private struct PongEvent : IGameEvent
        {
        }

        [Test]
        public void Raise_DeliversEvent_ToListener()
        {
            var bus = new GameEventBus();
            var got = 0;
            GenericEventBus<IGameEvent>.EventHandler<PingEvent> handler = (ref PingEvent e) => got = e.Value;
            bus.SubscribeTo(handler);

            bus.Raise(new PingEvent { Value = 42 });

            Assert.AreEqual(42, got);
        }

        [Test]
        public void GetListenerCount_TracksSubscribeAndUnsubscribe()
        {
            var bus = new GameEventBus();
            GenericEventBus<IGameEvent>.EventHandler<PingEvent> handler = (ref PingEvent e) => { };

            Assert.AreEqual(0, bus.GetListenerCount<PingEvent>());
            bus.SubscribeTo(handler);
            Assert.AreEqual(1, bus.GetListenerCount<PingEvent>());
            bus.UnsubscribeFrom(handler);
            Assert.AreEqual(0, bus.GetListenerCount<PingEvent>());
        }

        [Test]
        public void Raise_RecordsHistory_WithTypeAndListenerCount()
        {
            var bus = new GameEventBus();
            GenericEventBus<IGameEvent>.EventHandler<PingEvent> handler = (ref PingEvent e) => { };
            bus.SubscribeTo(handler);

            bus.Raise(new PingEvent { Value = 7 });

            var history = bus.GetHistory();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(typeof(PingEvent), history[0].EventType);
            Assert.AreEqual(1, history[0].ListenerCount);
            Assert.AreEqual("Ping(7)", history[0].Description);
        }

        [Test]
        public void History_IsCircular_DroppingOldest()
        {
            var bus = new GameEventBus(historyCapacity: 2);

            bus.Raise(new PingEvent { Value = 1 });
            bus.Raise(new PingEvent { Value = 2 });
            bus.Raise(new PingEvent { Value = 3 });

            var history = bus.GetHistory();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual("Ping(2)", history[0].Description); // mais antigo retido
            Assert.AreEqual("Ping(3)", history[1].Description); // mais recente
        }

        [Test]
        public void ClearHistory_EmptiesHistory_NotSubscriptions()
        {
            var bus = new GameEventBus();
            GenericEventBus<IGameEvent>.EventHandler<PingEvent> handler = (ref PingEvent e) => { };
            bus.SubscribeTo(handler);
            bus.Raise(new PingEvent { Value = 1 });

            bus.ClearHistory();

            Assert.AreEqual(0, bus.HistoryCount);
            Assert.AreEqual(1, bus.GetListenerCount<PingEvent>());
        }

        [Test]
        public void NestedRaise_RecordedInResolutionOrder()
        {
            // Um listener de Ping dispara Pong: a lib enfileira Pong e o libera após o Ping.
            // O histórico deve refletir a ordem real de resolução: Ping, depois Pong.
            var bus = new GameEventBus();
            GenericEventBus<IGameEvent>.EventHandler<PingEvent> onPing =
                (ref PingEvent e) => bus.Raise(new PongEvent());
            bus.SubscribeTo(onPing);

            bus.Raise(new PingEvent { Value = 1 });

            var history = bus.GetHistory();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(typeof(PingEvent), history[0].EventType);
            Assert.AreEqual(typeof(PongEvent), history[1].EventType);
        }
    }
}
