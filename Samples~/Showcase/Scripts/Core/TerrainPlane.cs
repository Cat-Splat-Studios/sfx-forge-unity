namespace SfxShowcase.Core
{
    /// <summary>
    /// Which of the two collision planes a piece of ground belongs to. A loop's path crosses itself, so its first half
    /// is solid only on plane A and its second half only on plane B; a switch at the top moves the player from one to
    /// the other. Everything else is on <see cref="Both"/>.
    /// </summary>
    public enum TerrainPlane
    {
        /// <summary>Solid for the player whatever plane it is on (ground); never a value for the player itself.</summary>
        Both = 0,
        /// <summary>The default plane: the half of a loop the player runs up first.</summary>
        A = 1,
        /// <summary>The half of a loop the player comes down.</summary>
        B = 2,
    }
}
