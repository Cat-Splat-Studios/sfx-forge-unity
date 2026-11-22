namespace SfxShowcase.Audio
{
    /// <summary>
    /// The three ways the game plays an SFX FORGE sound. Choosing one is the main decision for every sound:
    /// bake what is short, frequent and always the same; play live what the game shapes.
    /// </summary>
    public enum SoundRoute
    {
        /// <summary>
        /// Rendered once to an AudioClip with <c>SfxPreset.Bake</c> when the scene starts and played by an ordinary
        /// AudioSource. The cheapest route; volume (and the AudioSource's pitch) can still vary per trigger.
        /// </summary>
        Baked,

        /// <summary>
        /// Synthesized as it plays by an <c>SfxPlayer</c> from the voice pool, so every trigger can have its own pitch
        /// and velocity (the Chime's rising combo).
        /// </summary>
        LiveOneShot,

        /// <summary>
        /// A sustained patch on an <c>SfxPlayer</c> of its own, held while the game says so and bent every frame with
        /// the player's live pitch / cutoff / timbre / level values (the roll hum, the wind, the Wind-Up rev).
        /// </summary>
        LiveHeld,
    }
}
