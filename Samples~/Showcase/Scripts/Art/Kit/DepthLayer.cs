namespace SfxShowcase.Art
{
    /// <summary>
    /// Draw order, back to front. The value is the renderer's sorting order: nothing in the game writes depth, so this
    /// (and the order of the triangles inside one mesh) is the only thing that decides what covers what. The gaps leave
    /// room for the parts of one object (a Clipper's legs go one step behind its body).
    /// </summary>
    public enum DepthLayer
    {
        /// <summary>The sky gradient and the sun.</summary>
        Sky = -100,
        /// <summary>The farthest background wave.</summary>
        FarWave = -90,
        /// <summary>The middle background wave.</summary>
        MidWave = -80,
        /// <summary>The nearest background wave.</summary>
        NearWave = -70,
        /// <summary>Ground and platforms.</summary>
        Terrain = 0,
        /// <summary>The half of a loop the player runs up first: behind the player.</summary>
        LoopBack = 5,
        /// <summary>Chimes, Woofers, Static, Clippers, Tuning Forks, the Resonator.</summary>
        Props = 10,
        /// <summary>Brio.</summary>
        Character = 30,
        /// <summary>The half of a loop the player comes down: in front of the player.</summary>
        LoopFront = 40,
        /// <summary>Rings, shards, puffs, dust.</summary>
        Effects = 50,
        /// <summary>HUD panels; text and meters sit a few steps above.</summary>
        Hud = 100,
    }
}
