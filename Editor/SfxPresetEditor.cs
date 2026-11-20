// SPDX-License-Identifier: MIT
using UnityEditor;
using UnityEngine;

namespace CatSplat.SfxForge.Editor
{
    [CustomEditor(typeof(SfxPreset))]
    public class SfxPresetEditor : UnityEditor.Editor
    {
        static readonly string[] PadNames = BuildPadNames();
        static readonly System.Random Rng = new System.Random();
        static bool hearChanges = true, sendChanges;
        static int pad;

        static string[] BuildPadNames()
        {
            var n = new string[SfxProtocol.Slots];
            for (int i = 0; i < n.Length; i++) n[i] = "Pad " + (i + 1);
            return n;
        }

        public override void OnInspectorGUI()
        {
            var preset = (SfxPreset)target;
            SfxPatch p = preset.patch;

            EditorGUI.BeginChangeCheck();
            p.engine = (SfxEngine)EditorGUILayout.Popup("Engine", (int)p.engine, SfxPatch.EngineNames);
            EditorGUILayout.Space();
            for (int i = 0; i < SfxPatch.ParamCount; i++)
            {
                var k = (SfxParam)i;
                if (i == (int)SfxParam.Attack) { EditorGUILayout.Space(); EditorGUILayout.LabelField("Envelopes", EditorStyles.boldLabel); }
                if (i == (int)SfxParam.LfoRate) { EditorGUILayout.Space(); EditorGUILayout.LabelField("LFO", EditorStyles.boldLabel); }
                if (i == (int)SfxParam.Spread) { EditorGUILayout.Space(); EditorGUILayout.LabelField("Effects and level", EditorStyles.boldLabel); }
                if (k == SfxParam.Table) { if (p.engine == SfxEngine.Wavetable) p.table = (byte)EditorGUILayout.Popup("Table", p.table % SfxPatch.TableCount, SfxPatch.TableNames); }
                else if (k == SfxParam.Spread && p.engine != SfxEngine.Wavetable) { }
                else p[k] = (byte)EditorGUILayout.IntSlider(SfxPatch.Label(k, p.engine), p[k], 0, 127);
            }
            float hold = preset.holdSeconds;
            if (p.IsSustained) hold = Mathf.Max(0.05f, EditorGUILayout.FloatField(new GUIContent("Hold Seconds", "How long a baked clip or a preview holds this sound."), hold));
            bool edited = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play", GUILayout.Height(26))) SfxForgeEditorLink.Preview(p, p.IsSustained ? hold : 0f);
                if (GUILayout.Button(new GUIContent("Mutate", "Every value moves by up to 15 % – a variation of this sound."), GUILayout.Height(26))) { p.Mutate(Rng); edited = true; }
                if (GUILayout.Button(new GUIContent("Bake WAV", "Writes the sound as a .wav next to this asset."), GUILayout.Height(26)))
                {
                    var clip = SfxForgeEditorLink.BakeWav(preset);
                    if (clip != null) EditorGUIUtility.PingObject(clip);
                }
            }
            hearChanges = EditorGUILayout.ToggleLeft("Play when a value changes", hearChanges);

            if (edited)
            {
                Undo.RecordObject(preset, "Edit SFX Preset");
                preset.patch = p; preset.holdSeconds = hold;
                EditorUtility.SetDirty(preset);
                if (hearChanges && !Application.isPlaying) SfxForgeEditorLink.Preview(p, p.IsSustained ? Mathf.Min(hold, 0.8f) : 0f);
                if (sendChanges && SfxLink.Shared.HasOutput) SfxLink.Shared.SendPatch(pad, p);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Device", EditorStyles.boldLabel);
            if (!SfxLink.Shared.HasOutput)
            {
                EditorGUILayout.HelpBox("Not connected. Choose the MIDI ports in Window > SFX Forge.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                pad = EditorGUILayout.Popup(pad, PadNames, GUILayout.Width(80));
                if (GUILayout.Button("Send to pad")) SfxLink.Shared.SendPatch(pad, p);
                if (GUILayout.Button("Play on device")) { SfxLink.Shared.SendPatch(pad, p); SfxLink.Shared.PlayPad(pad); SfxLink.Shared.ReleasePad(pad); }
            }
            sendChanges = EditorGUILayout.ToggleLeft("Send to that pad when a value changes", sendChanges);
        }
    }
}
