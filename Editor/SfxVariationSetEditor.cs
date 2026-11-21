// SPDX-License-Identifier: MIT
using UnityEditor;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>The Inspector of a variation set: its values as the device's tiles, its variants to listen to, BAKE (SfxVariationSetPanel).</summary>
    [CustomEditor(typeof(SfxVariationSet))]
    [CanEditMultipleObjects]
    public class SfxVariationSetEditor : UnityEditor.Editor
    {
        /// <summary>The whole Inspector as one element: what CreateInspectorGUI returns, and what a test or a picture can build too.</summary>
        public static VisualElement Build(SerializedObject serializedObject) => new SfxVariationSetPanel(serializedObject);

        public override VisualElement CreateInspectorGUI() => Build(serializedObject);
    }
}
