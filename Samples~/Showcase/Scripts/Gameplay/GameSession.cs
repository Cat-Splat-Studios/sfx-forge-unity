using UnityEngine;
using SfxShowcase.Core;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// The run: title card, playing, finished. It keeps what a run is scored by (Chimes carried, time, top speed),
    /// counts the Chime combo, and turns Jump on the title card and on the results panel into "start" and "play again".
    /// A restart happens in place: the player goes back to the start and <see cref="GameEvents.GameStarted"/> is raised,
    /// which is the level pieces' cue to put themselves back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSession : GameSessionBase
    {
        [Tooltip("The player whose run this is.")]
        [SerializeField] PlayerController player;

        [Tooltip("Seconds the results panel stays before Jump is accepted, so the Jump held while arriving does not skip it.")]
        [SerializeField] float resultsHoldSeconds = 1f;

        GamePhase phase = GamePhase.Title;
        int chimeCount;
        float elapsedSeconds;
        float topSpeedReached;
        float finishedSeconds;
        int chimeCombo;
        float lastChimeTime;
        bool previousJumpHeld = true;

        /// <inheritdoc/>
        public override GamePhase Phase => phase;
        /// <inheritdoc/>
        public override int ChimeCount => chimeCount;
        /// <inheritdoc/>
        public override float ElapsedSeconds => elapsedSeconds;
        /// <inheritdoc/>
        public override float TopSpeedReached => topSpeedReached;
        /// <inheritdoc/>
        public override SectionId CurrentSection => Sections.At(player.Position.x);

        /// <summary>Connects the session to its player; called once by <see cref="GameplayInstaller"/> while the scene is built.</summary>
        public void Wire(PlayerController sessionPlayer)
        {
            player = sessionPlayer;
        }

        /// <inheritdoc/>
        public override void StartGame()
        {
            if (phase != GamePhase.Title) return;
            BeginRun();
        }

        /// <inheritdoc/>
        public override void Restart()
        {
            player.ResetForNewRun();
            BeginRun();
        }

        /// <summary>
        /// Adds Chimes to the carried count and raises <see cref="GameEvents.ChimeCollected"/> once per Chime. Each
        /// Chime within <see cref="DesignTargets.ChimeComboWindow"/> of the last one raises the combo by one.
        /// </summary>
        public void CollectChimes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                bool continuesCombo = chimeCombo > 0 && Time.time - lastChimeTime <= DesignTargets.ChimeComboWindow;
                chimeCombo = continuesCombo ? chimeCombo + 1 : 1;
                lastChimeTime = Time.time;
                chimeCount++;
                GameEvents.RaiseChimeCollected(chimeCombo);
            }
        }

        /// <summary>Takes every carried Chime away (a hit, or a respawn) and returns how many there were.</summary>
        public int DropAllChimes()
        {
            int dropped = chimeCount;
            chimeCount = 0;
            chimeCombo = 0;
            return dropped;
        }

        /// <summary>The goal was reached: the timer stops and the results are shown. Called by the player from Finish.</summary>
        public void EndRun()
        {
            if (phase != GamePhase.Playing) return;
            phase = GamePhase.Finished;
            finishedSeconds = 0f;
        }

        void Update()
        {
            bool jumpHeld = player.ActiveInput.JumpHeld;
            bool jumpPressed = jumpHeld && !previousJumpHeld;
            previousJumpHeld = jumpHeld;

            switch (phase)
            {
                case GamePhase.Title:
                    if (jumpPressed) Confirm();
                    break;
                case GamePhase.Playing:
                    elapsedSeconds += Time.deltaTime;
                    topSpeedReached = Mathf.Max(topSpeedReached, player.Speed);
                    break;
                case GamePhase.Finished:
                    finishedSeconds += Time.deltaTime;
                    if (jumpPressed && finishedSeconds >= resultsHoldSeconds) Confirm();
                    break;
            }
        }

        /// <summary>Jump on the title card starts the run; Jump on the results panel starts it again.</summary>
        void Confirm()
        {
            GameEvents.RaiseUiConfirmed();
            if (phase == GamePhase.Title) StartGame();
            else Restart();
        }

        void BeginRun()
        {
            chimeCount = 0;
            chimeCombo = 0;
            elapsedSeconds = 0f;
            topSpeedReached = 0f;
            phase = GamePhase.Playing;
            player.SetControl(true);
            GameEvents.RaiseGameStarted();
        }
    }
}
