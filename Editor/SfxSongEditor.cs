// SPDX-License-Identifier: MIT
using UnityEditor;
using UnityEngine;

namespace CatSplat.SfxForge.Editor
{
    [CustomEditor(typeof(SfxSong))]
    public class SfxSongEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var song = (SfxSong)target;
            if (!song.IsValid)
            {
                EditorGUILayout.HelpBox("No song yet. Make one on SFX FORGE and press SEND on its SONG page (or 'Fetch the song' in Window > SFX Forge) – it arrives as an asset like this one.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("Tempo", song.Bpm + " BPM");
            EditorGUILayout.LabelField("Pattern length", song.StepsPerPattern + " steps");
            EditorGUILayout.LabelField("Chain", song.Chain <= 1 ? "stays on one pattern" : "patterns A to " + (char)('A' + song.Chain - 1));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sounds and their layers", EditorStyles.boldLabel);
            for (int i = 0; i < SfxProtocol.Slots; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    float layer = song.Layer(i);
                    EditorGUILayout.LabelField($"{i + 1}  {song.sounds[i].engine}", GUILayout.Width(110));
                    EditorGUILayout.LabelField(layer <= 0f ? "always plays" : $"from intensity {layer:0.00}");
                    if (GUILayout.Button("Play", GUILayout.Width(44))) SfxForgeEditorLink.Preview(song.sounds[i]);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!SfxLink.Shared.HasOutput))
                if (GUILayout.Button(new GUIContent("Send to device", "Replaces the 16 sounds and the song on the device."))) SfxLink.Shared.SendSong(song.sounds, song.data);
            if (!SfxLink.Shared.HasOutput) EditorGUILayout.HelpBox("Not connected. Choose the MIDI ports in Window > SFX Forge.", MessageType.None);
        }
    }
}
