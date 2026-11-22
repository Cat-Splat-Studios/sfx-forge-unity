using System;
using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The one seam between the game and its sound: gameplay and level code RAISE these events, the audio (and the HUD)
    /// LISTEN. Nothing here knows about SFX FORGE, so the game compiles and plays silently without the audio folder.
    /// Subscribe in OnEnable and unsubscribe in OnDisable. Values named "01" are normalised 0..1.
    /// Continuous sounds (the roll hum, the wind) have no event: the audio reads
    /// <see cref="IPlayerState"/> every frame.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>The title card was dismissed and the run begins (also after a restart).</summary>
        public static event Action GameStarted;

        /// <summary>The player left the ground by jumping.</summary>
        public static event Action Jumped;

        /// <summary>The player touched ground after being airborne; the speed into the ground in m/s.</summary>
        public static event Action<float> Landed;

        /// <summary>One foot came down while running (not tucked); speed ÷ top speed.</summary>
        public static event Action<float> Footstep;

        /// <summary>The player tucked into a roll on the ground.</summary>
        public static event Action TuckStarted;

        /// <summary>One charge tap of the Wind-Up; the charge after the tap, 0..1.</summary>
        public static event Action<float> WindUpCharged;

        /// <summary>The Wind-Up was released into a Burst; the charge it had, 0..1.</summary>
        public static event Action<float> BurstReleased;

        /// <summary>
        /// A Chime was collected; combo is 1 for the first and one more for each within the combo window.
        /// </summary>
        public static event Action<int> ChimeCollected;

        /// <summary>The player was hit while carrying Chimes; how many were scattered.</summary>
        public static event Action<int> ChimesScattered;

        /// <summary>A Woofer fired.</summary>
        public static event Action WooferBounced;

        /// <summary>The player took a hit (with or without Chimes).</summary>
        public static event Action Hurt;

        /// <summary>A Clipper was popped.</summary>
        public static event Action ClipperDefeated;

        /// <summary>A Tuning Fork was lit for the first time; its index, 0 = the first in the level.</summary>
        public static event Action<int> CheckpointReached;

        /// <summary>The player ran through the Resonator.</summary>
        public static event Action GoalReached;

        /// <summary>The player braked hard against its motion on the ground.</summary>
        public static event Action Skidded;

        /// <summary>The player reappeared at a Tuning Fork (or the start); the section it stands in.</summary>
        public static event Action<SectionId> Respawned;

        /// <summary>Jump was pressed on the title card or the results panel.</summary>
        public static event Action UiConfirmed;

        /// <summary>The player crossed into a section of the level.</summary>
        public static event Action<SectionId> SectionEntered;

        /// <summary>
        /// The player left the launch ramp at the end of Overdrive: the music's cue for its turnaround.
        /// </summary>
        public static event Action FinalLaunch;

        /// <summary>Raises <see cref="GameStarted"/>.</summary>
        public static void RaiseGameStarted() => GameStarted?.Invoke();

        /// <summary>Raises <see cref="Jumped"/>.</summary>
        public static void RaiseJumped() => Jumped?.Invoke();

        /// <summary>Raises <see cref="Landed"/>.</summary>
        public static void RaiseLanded(float impactSpeed) => Landed?.Invoke(impactSpeed);

        /// <summary>Raises <see cref="Footstep"/>.</summary>
        public static void RaiseFootstep(float speed01) => Footstep?.Invoke(speed01);

        /// <summary>Raises <see cref="TuckStarted"/>.</summary>
        public static void RaiseTuckStarted() => TuckStarted?.Invoke();

        /// <summary>Raises <see cref="WindUpCharged"/>.</summary>
        public static void RaiseWindUpCharged(float charge01) => WindUpCharged?.Invoke(charge01);

        /// <summary>Raises <see cref="BurstReleased"/>.</summary>
        public static void RaiseBurstReleased(float charge01) => BurstReleased?.Invoke(charge01);

        /// <summary>Raises <see cref="ChimeCollected"/>.</summary>
        public static void RaiseChimeCollected(int combo) => ChimeCollected?.Invoke(combo);

        /// <summary>Raises <see cref="ChimesScattered"/>.</summary>
        public static void RaiseChimesScattered(int count) => ChimesScattered?.Invoke(count);

        /// <summary>Raises <see cref="WooferBounced"/>.</summary>
        public static void RaiseWooferBounced() => WooferBounced?.Invoke();

        /// <summary>Raises <see cref="Hurt"/>.</summary>
        public static void RaiseHurt() => Hurt?.Invoke();

        /// <summary>Raises <see cref="ClipperDefeated"/>.</summary>
        public static void RaiseClipperDefeated() => ClipperDefeated?.Invoke();

        /// <summary>Raises <see cref="CheckpointReached"/>.</summary>
        public static void RaiseCheckpointReached(int index) => CheckpointReached?.Invoke(index);

        /// <summary>Raises <see cref="GoalReached"/>.</summary>
        public static void RaiseGoalReached() => GoalReached?.Invoke();

        /// <summary>Raises <see cref="Skidded"/>.</summary>
        public static void RaiseSkidded() => Skidded?.Invoke();

        /// <summary>Raises <see cref="Respawned"/>.</summary>
        public static void RaiseRespawned(SectionId section) => Respawned?.Invoke(section);

        /// <summary>Raises <see cref="UiConfirmed"/>.</summary>
        public static void RaiseUiConfirmed() => UiConfirmed?.Invoke();

        /// <summary>Raises <see cref="SectionEntered"/>.</summary>
        public static void RaiseSectionEntered(SectionId section) => SectionEntered?.Invoke(section);

        /// <summary>Raises <see cref="FinalLaunch"/>.</summary>
        public static void RaiseFinalLaunch() => FinalLaunch?.Invoke();

        /// <summary>
        /// Drops every listener when play mode starts, so nothing from a previous run is called when the editor enters
        /// play mode without reloading the domain.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearListeners()
        {
            GameStarted = null;
            Jumped = null;
            Landed = null;
            Footstep = null;
            TuckStarted = null;
            WindUpCharged = null;
            BurstReleased = null;
            ChimeCollected = null;
            ChimesScattered = null;
            WooferBounced = null;
            Hurt = null;
            ClipperDefeated = null;
            CheckpointReached = null;
            GoalReached = null;
            Skidded = null;
            Respawned = null;
            UiConfirmed = null;
            SectionEntered = null;
            FinalLaunch = null;
        }
    }
}
