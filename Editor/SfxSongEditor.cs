// SPDX-License-Identifier: MIT
using UnityEditor;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// The Inspector of a song: the device's SONG, steps, MIX, MASTER and AUTO as one panel (SfxSongPanel).
    /// </summary>
    [CustomEditor(typeof(SfxSong))]
    [CanEditMultipleObjects]
    public class SfxSongEditor : UnityEditor.Editor
    {
        /// <summary>
        /// The whole Inspector as one element: what CreateInspectorGUI returns, and what a test or a
        /// picture can build too.
        /// </summary>
        public static VisualElement Build(SerializedObject serializedObject) => new SfxSongPanel(serializedObject);

        public override VisualElement CreateInspectorGUI() => Build(serializedObject);
    }
}
