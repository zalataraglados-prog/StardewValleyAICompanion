using System.Collections.Generic;

namespace StardewAI.RuntimePrimitives
{
    public sealed class NativeHeavyHitterProgress
    {
        private readonly List<int> observedHealth = new List<int>();

        public NativeHeavyHitterProgress(int healthBefore, int maxSwings)
        {
            MaxSwings = maxSwings;
            observedHealth.Add(healthBefore);
        }

        public int MaxSwings { get; }
        public int SwingCount { get; private set; }
        public bool ActionIssued { get; private set; }
        public IReadOnlyList<int> ObservedHealth => observedHealth;

        public bool CanIssueAction()
        {
            return !ActionIssued && SwingCount < MaxSwings;
        }

        public void MarkActionIssued()
        {
            if (CanIssueAction())
            {
                ActionIssued = true;
            }
        }

        public void RecordCompletedSwing(int? remainingHealth)
        {
            if (!ActionIssued)
            {
                return;
            }

            SwingCount++;
            ActionIssued = false;
            RecordHealth(remainingHealth);
        }

        public void RecordRemoval()
        {
            RecordCompletedSwing(0);
            RecordHealth(0);
        }

        private void RecordHealth(int? health)
        {
            if (health.HasValue && (observedHealth.Count == 0 || observedHealth[observedHealth.Count - 1] != health.Value))
            {
                observedHealth.Add(health.Value);
            }
        }
    }
}
