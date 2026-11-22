using CatSplat.SfxForge;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// A sustained patch on an <see cref="SfxPlayer"/> of its own, remembered as held or let go so it is never started
    /// twice. While it is held, the owner bends it through the player's live values (pitchSemitones, cutoffOctaves, fm,
    /// level), which the player glides to over 30 ms – so they can be written every frame without clicks.
    /// </summary>
    sealed class HeldSound
    {
        readonly int midiNote;
        bool held;

        /// <summary>The SfxPlayer that plays the sound (may be null: then nothing happens).</summary>
        public SfxPlayer Player { get; }

        /// <summary>Whether the sound is being held now.</summary>
        public bool IsHeld => held;

        /// <summary>
        /// Wraps the player whose preset is the held sound. <paramref name="midiNote"/> is the note it is held at, so a
        /// pitched hum is in tune with the song; 0 holds it at the patch's own FREQ (right for noise such as wind).
        /// </summary>
        public HeldSound(SfxPlayer player, int midiNote)
        {
            Player = player;
            this.midiNote = midiNote;
        }

        /// <summary>Starts the sound if it is not held already.</summary>
        public void Hold()
        {
            if (held || Player == null)
            {
                return;
            }

            Player.Play(1f, 0f, midiNote);
            held = true;
        }

        /// <summary>Lets the sound go: it fades over the patch's RELEASE time.</summary>
        public void LetGo()
        {
            if (!held)
            {
                return;
            }

            Player.Release();
            held = false;
        }

        /// <summary>Lets go and starts again at once: a new attack on a sound that keeps going.</summary>
        public void Retrigger()
        {
            LetGo();
            Hold();
        }
    }
}
