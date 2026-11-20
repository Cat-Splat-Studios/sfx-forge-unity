using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Creates the game's two materials. Both use "Sprites/Default", which every built-in render pipeline project has
    /// (it is in the Always Included Shaders list): unlit, coloured by the mesh's vertex colours, drawing both faces
    /// and writing no depth. Nothing is ever tinted through a material.
    /// </summary>
    public static class ArtMaterials
    {
        const string ShaderName = "Sprites/Default";

        /// <summary>
        /// A new unlit vertex-colour material. The opaque and the transparent material are the same kind; they are kept
        /// apart so that geometry with alpha below 1 is easy to find and is never batched in between solid geometry.
        /// </summary>
        public static Material CreateUnlit(string name)
        {
            return new Material(Shader.Find(ShaderName)) { name = name };
        }
    }
}
