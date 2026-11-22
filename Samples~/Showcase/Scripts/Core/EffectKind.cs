namespace SfxShowcase.Core
{
    /// <summary>The short visual bursts the art kit can spawn at a point (<see cref="IArtKit.SpawnEffect"/>).</summary>
    public enum EffectKind
    {
        /// <summary>The small expanding cyan circle of a collected Chime.</summary>
        ChimePop = 0,

        /// <summary>The four square shards of a popped Clipper.</summary>
        ClipperShards = 1,

        /// <summary>The puff behind the player when a Burst is released.</summary>
        BurstPuff = 2,

        /// <summary>The dust of a hard landing or a skid.</summary>
        Dust = 3,
    }
}
