namespace SfxShowcase.Art
{
    /// <summary>
    /// The measurements of the art direction (DESIGN.md section 4, "Line and outline rules"), in metres. Every
    /// generator reads them from here, so the look changes in one place.
    /// </summary>
    public static class ArtStyle
    {
        /// <summary>The Ink outline around everything you can touch except the ground.</summary>
        public const float OutlineWidth = 0.06f;
        /// <summary>The cream line along every walkable surface.</summary>
        public const float TopLineThickness = 0.2f;
        /// <summary>The height of one strata band inside the terrain body.</summary>
        public const float StrataThickness = 0.6f;
        /// <summary>The distance from one strata band to the next, in world space (bands stay level).</summary>
        public const float StrataSpacing = 1.8f;
        /// <summary>How many segments a full circle is drawn with.</summary>
        public const int CircleSegments = 32;
        /// <summary>Screen half-height in HUD units, which is also metres at the design camera size (DESIGN.md section 2).</summary>
        public const float ViewHalfHeight = 9f;
    }
}
