using System;
using System.Collections.Generic;
using SfxShowcase.Core;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// Counts the <see cref="GameEvents"/> a test cares about, the way the audio folder listens to them: subscribe when
    /// created, unsubscribe when disposed.
    /// </summary>
    public sealed class GameEventLog : IDisposable
    {
        /// <summary>How often a run started (the title card dismissed, or a restart).</summary>
        public int GameStarts { get; private set; }

        /// <summary>How often the player jumped.</summary>
        public int Jumps { get; private set; }

        /// <summary>How many charge taps the Wind-Up took.</summary>
        public int WindUpTaps { get; private set; }

        /// <summary>How often a Burst was released.</summary>
        public int Bursts { get; private set; }

        /// <summary>The charge of the last Burst, 0..1.</summary>
        public float LastBurstCharge { get; private set; }

        /// <summary>How many Chimes were collected.</summary>
        public int ChimesCollected { get; private set; }

        /// <summary>The highest combo a collected Chime reported.</summary>
        public int HighestCombo { get; private set; }

        /// <summary>How many Chimes the last hit scattered.</summary>
        public int LastScatterCount { get; private set; }

        /// <summary>How often the player was hurt.</summary>
        public int Hurts { get; private set; }

        /// <summary>How often a Woofer fired.</summary>
        public int WooferBounces { get; private set; }

        /// <summary>How often the player respawned.</summary>
        public int Respawns { get; private set; }

        /// <summary>How often the goal was reached.</summary>
        public int GoalsReached { get; private set; }

        /// <summary>How often the final launch was announced.</summary>
        public int FinalLaunches { get; private set; }

        /// <summary>The Tuning Forks lit, by index, in the order they were reached.</summary>
        public List<int> Checkpoints { get; } = new List<int>();

        /// <summary>Every section announced, in order.</summary>
        public List<SectionId> Sections { get; } = new List<SectionId>();

        /// <summary>Starts listening.</summary>
        public GameEventLog()
        {
            GameEvents.GameStarted += OnGameStarted;
            GameEvents.Jumped += OnJumped;
            GameEvents.WindUpCharged += OnWindUpCharged;
            GameEvents.BurstReleased += OnBurstReleased;
            GameEvents.ChimeCollected += OnChimeCollected;
            GameEvents.ChimesScattered += OnChimesScattered;
            GameEvents.Hurt += OnHurt;
            GameEvents.WooferBounced += OnWooferBounced;
            GameEvents.Respawned += OnRespawned;
            GameEvents.GoalReached += OnGoalReached;
            GameEvents.FinalLaunch += OnFinalLaunch;
            GameEvents.CheckpointReached += OnCheckpointReached;
            GameEvents.SectionEntered += OnSectionEntered;
        }

        /// <summary>Stops listening.</summary>
        public void Dispose()
        {
            GameEvents.GameStarted -= OnGameStarted;
            GameEvents.Jumped -= OnJumped;
            GameEvents.WindUpCharged -= OnWindUpCharged;
            GameEvents.BurstReleased -= OnBurstReleased;
            GameEvents.ChimeCollected -= OnChimeCollected;
            GameEvents.ChimesScattered -= OnChimesScattered;
            GameEvents.Hurt -= OnHurt;
            GameEvents.WooferBounced -= OnWooferBounced;
            GameEvents.Respawned -= OnRespawned;
            GameEvents.GoalReached -= OnGoalReached;
            GameEvents.FinalLaunch -= OnFinalLaunch;
            GameEvents.CheckpointReached -= OnCheckpointReached;
            GameEvents.SectionEntered -= OnSectionEntered;
        }

        void OnGameStarted() => GameStarts++;

        void OnJumped() => Jumps++;

        void OnWindUpCharged(float charge01) => WindUpTaps++;

        void OnBurstReleased(float charge01)
        {
            Bursts++;
            LastBurstCharge = charge01;
        }

        void OnChimeCollected(int combo)
        {
            ChimesCollected++;
            HighestCombo = Math.Max(HighestCombo, combo);
        }

        void OnChimesScattered(int count) => LastScatterCount = count;

        void OnHurt() => Hurts++;

        void OnWooferBounced() => WooferBounces++;

        void OnRespawned(SectionId section) => Respawns++;

        void OnGoalReached() => GoalsReached++;

        void OnFinalLaunch() => FinalLaunches++;

        void OnCheckpointReached(int index) => Checkpoints.Add(index);

        void OnSectionEntered(SectionId section) => Sections.Add(section);
    }
}
