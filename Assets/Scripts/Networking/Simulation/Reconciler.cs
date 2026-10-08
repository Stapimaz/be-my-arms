using System.Collections.Generic;

namespace BeMyArms.Networking
{
    /// <summary>
    /// Client-side prediction and reconciliation for the P1-owned portion of the body. NGO has no
    /// built-in prediction, so this is the custom layer Networking must prove: predict locally from inputs,
    /// then on each authoritative snapshot reset to the server state at the last acknowledged input
    /// and replay the not-yet-acknowledged inputs.
    ///
    /// Pure and unit-testable; the MonoBehaviour glue feeds it and reads Predicted.
    /// </summary>
    public class Reconciler
    {
        struct Entry
        {
            public uint Sequence;
            public P1Input Input;
            public float DeltaTime;
        }

        readonly List<Entry> _history = new List<Entry>();

        public BodyState Predicted;
        public int PendingInputCount => _history.Count;

        public void Reset(in BodyState authoritative)
        {
            Predicted = authoritative;
            _history.Clear();
        }

        /// <summary>Advance the local predicted state with a freshly sampled input and remember it.</summary>
        public void Predict(in P1Input input, float deltaTime, BodySim sim)
        {
            _history.Add(new Entry { Sequence = input.Sequence, Input = input, DeltaTime = deltaTime });
            sim.State = Predicted;
            sim.ApplyP1(input, deltaTime);
            Predicted = sim.State;
        }

        /// <summary>
        /// Reset to the authoritative state and replay every input newer than the server's last
        /// acknowledged sequence. Inputs at or before the ack are dropped (they are already in the
        /// authoritative state), which is what keeps the buffer bounded.
        /// </summary>
        public void Reconcile(in BodyState authoritative, uint lastAckedSequence, BodySim sim)
        {
            Predicted = authoritative;
            for (int i = 0; i < _history.Count; i++)
            {
                Entry entry = _history[i];
                if (entry.Sequence <= lastAckedSequence) continue;
                sim.State = Predicted;
                sim.ApplyP1(entry.Input, entry.DeltaTime);
                Predicted = sim.State;
            }

            _history.RemoveAll(e => e.Sequence <= lastAckedSequence);
        }
    }
}
