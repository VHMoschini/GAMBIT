using System.Collections.Generic;
using Game.Core.Fsm;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class StateMachineTests
    {
        private enum S { A, B, C, GameOver }

        /// <summary>Contexto que grava a sequência de callbacks recebidos, para asserir ordem e coerência.</summary>
        private sealed class Recorder
        {
            public readonly List<string> Log = new List<string>();
        }

        /// <summary>Estado que registra cada callback no <see cref="Recorder"/> com seu nome.</summary>
        private sealed class RecordingState : IState<Recorder>
        {
            private readonly string _name;
            public RecordingState(string name) => _name = name;
            public void OnEnter(Recorder c) => c.Log.Add($"enter:{_name}");
            public void OnExit(Recorder c) => c.Log.Add($"exit:{_name}");
            public void OnUpdate(Recorder c) => c.Log.Add($"update:{_name}");
        }

        private static StateMachine<S, Recorder> BuildLinear(Recorder rec)
        {
            var fsm = new StateMachine<S, Recorder>(rec);
            fsm.AddState(S.A, new RecordingState("A"));
            fsm.AddState(S.B, new RecordingState("B"));
            fsm.AddState(S.C, new RecordingState("C"));
            fsm.AddState(S.GameOver, new RecordingState("GameOver"));
            fsm.AddTransition(S.A, S.B);
            fsm.AddTransition(S.B, S.C);
            fsm.AddAnyTransition(S.GameOver); // AnyState → GameOver
            return fsm;
        }

        [Test]
        public void SetInitialState_Enters_WithoutRequiringTransition()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);

            fsm.SetInitialState(S.A);

            Assert.IsTrue(fsm.IsRunning);
            Assert.AreEqual(S.A, fsm.CurrentStateId);
            Assert.AreEqual(1, fsm.StackDepth);
            CollectionAssert.AreEqual(new[] { "enter:A" }, rec.Log);
        }

        [Test]
        public void SetInitialState_DoesNotFire_OnStateChanged()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            var fired = false;
            fsm.OnStateChanged += (_, _) => fired = true;

            fsm.SetInitialState(S.A);

            Assert.IsFalse(fired, "Não há estado anterior na entrada inicial.");
        }

        [Test]
        public void SetInitialState_Twice_Throws()
        {
            var fsm = BuildLinear(new Recorder());
            fsm.SetInitialState(S.A);
            Assert.Throws<System.InvalidOperationException>(() => fsm.SetInitialState(S.B));
        }

        [Test]
        public void ChangeState_Valid_RunsExitBeforeEnter_AndFiresChanged()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);

            S fromArg = default, toArg = default;
            fsm.OnStateChanged += (from, to) => { fromArg = from; toArg = to; };

            fsm.ChangeState(S.B);

            // OnExit do antigo antes do OnEnter do novo.
            CollectionAssert.AreEqual(new[] { "enter:A", "exit:A", "enter:B" }, rec.Log);
            Assert.AreEqual(S.B, fsm.CurrentStateId);
            Assert.AreEqual(S.A, fromArg);
            Assert.AreEqual(S.B, toArg);
        }

        [Test]
        public void ChangeState_OnStateChanged_FiresBetweenExitAndEnter()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);
            fsm.OnStateChanged += (_, _) => rec.Log.Add("changed");

            fsm.ChangeState(S.B);

            CollectionAssert.AreEqual(new[] { "enter:A", "exit:A", "changed", "enter:B" }, rec.Log);
        }

        [Test]
        public void ChangeState_Undeclared_Throws_AndStateUnchanged()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);

            Assert.Throws<InvalidStateTransitionException>(() => fsm.ChangeState(S.C));
            Assert.AreEqual(S.A, fsm.CurrentStateId);
            CollectionAssert.AreEqual(new[] { "enter:A" }, rec.Log);
        }

        [Test]
        public void ChangeState_ToUnregisteredState_Throws()
        {
            var rec = new Recorder();
            var fsm = new StateMachine<S, Recorder>(rec);
            fsm.AddState(S.A, new RecordingState("A"));
            fsm.SetInitialState(S.A);

            Assert.Throws<InvalidStateTransitionException>(() => fsm.ChangeState(S.B));
        }

        [Test]
        public void AnyTransition_AllowedFromAnyState()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);
            fsm.ChangeState(S.B); // A→B declarada

            Assert.DoesNotThrow(() => fsm.ChangeState(S.GameOver)); // B→GameOver via AnyState
            Assert.AreEqual(S.GameOver, fsm.CurrentStateId);
        }

        [Test]
        public void ChangeState_BeforeRunning_Throws()
        {
            var fsm = BuildLinear(new Recorder());
            Assert.Throws<System.InvalidOperationException>(() => fsm.ChangeState(S.B));
        }

        [Test]
        public void Tick_InvokesUpdate_OfCurrentState()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);
            rec.Log.Clear();

            fsm.Tick();

            CollectionAssert.AreEqual(new[] { "update:A" }, rec.Log);
        }

        [Test]
        public void DelegateState_InvokesCallbacks()
        {
            var rec = new Recorder();
            var fsm = new StateMachine<S, Recorder>(rec);
            fsm.AddState(S.A, onEnter: c => c.Log.Add("enterA"), onExit: c => c.Log.Add("exitA"));
            fsm.AddState(S.B, onEnter: c => c.Log.Add("enterB"));
            fsm.AddTransition(S.A, S.B);

            fsm.SetInitialState(S.A);
            fsm.ChangeState(S.B);

            CollectionAssert.AreEqual(new[] { "enterA", "exitA", "enterB" }, rec.Log);
        }

        // ---- Pilha (wizard) ----------------------------------------------------

        [Test]
        public void PushPop_PreservesCoherence()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);

            // Push avança: A suspenso (sem exit), B entra.
            fsm.PushState(S.B);
            Assert.AreEqual(S.B, fsm.CurrentStateId);
            Assert.AreEqual(2, fsm.StackDepth);

            // Push de novo: B suspenso, C entra (B→C declarada).
            fsm.PushState(S.C);
            Assert.AreEqual(S.C, fsm.CurrentStateId);
            Assert.AreEqual(3, fsm.StackDepth);

            // Pop: C sai, B revelado sem re-enter.
            fsm.PopState();
            Assert.AreEqual(S.B, fsm.CurrentStateId);
            Assert.AreEqual(2, fsm.StackDepth);

            // Pop: B sai, A revelado sem re-enter — voltamos coerentes ao inicial.
            fsm.PopState();
            Assert.AreEqual(S.A, fsm.CurrentStateId);
            Assert.AreEqual(1, fsm.StackDepth);

            // Cada estado: exatamente um enter e um exit; revelados não re-entram.
            CollectionAssert.AreEqual(
                new[] { "enter:A", "enter:B", "enter:C", "exit:C", "exit:B" },
                rec.Log);
        }

        [Test]
        public void PushState_Undeclared_Throws()
        {
            var rec = new Recorder();
            var fsm = BuildLinear(rec);
            fsm.SetInitialState(S.A);
            Assert.Throws<InvalidStateTransitionException>(() => fsm.PushState(S.C)); // A→C não declarada
        }

        [Test]
        public void PopState_OnInitialState_Throws()
        {
            var fsm = BuildLinear(new Recorder());
            fsm.SetInitialState(S.A);
            Assert.Throws<System.InvalidOperationException>(() => fsm.PopState());
        }
    }
}
