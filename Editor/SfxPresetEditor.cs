// SPDX-License-Identifier: MIT
using UnityEditor;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>The Inspector of a sound: the device's own pages (SfxPresetPanel).</summary>
    [CustomEditor(typeof(SfxPreset))]
    [CanEditMultipleObjects]
    public class SfxPresetEditor : UnityEditor.Editor
    {
        /// <summary>
        /// The whole Inspector as one element: what CreateInspectorGUI returns, and what a test or a
        /// picture can build too.
        /// </summary>
        public static VisualElement Build(SerializedObject serializedObject) => new SfxPresetPanel(serializedObject);

        public override VisualElement CreateInspectorGUI() => Build(serializedObject);
    }
}
