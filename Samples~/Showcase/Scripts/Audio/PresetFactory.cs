using CatSplat.SfxForge;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// Makes the <see cref="SfxPreset"/> for one sound of the library. The scene builder passes a factory that writes
    /// each preset as an asset (so every sound can be clicked and tuned in the Inspector) while this assembly stays
    /// free of editor code; passing null to <see cref="AudioInstaller.Install"/> makes them in memory instead.
    /// </summary>
    /// <param name="name">The asset's name, e.g. "Chime".</param>
    /// <param name="patch">The patch from <see cref="SoundLibrary"/>.</param>
    /// <param name="holdSeconds">For a held patch: how long a baked clip holds it.</param>
    public delegate SfxPreset PresetFactory(string name, SfxPatch patch, float holdSeconds);
}
