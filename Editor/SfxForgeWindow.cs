// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>Window > SFX Forge: the MIDI ports, the device's 16 pads, and the patches it sends – as assets.</summary>
    public class SfxForgeWindow : EditorWindow
    {
        const string FolderPref = "SfxForge.Folder", AutoSavePref = "SfxForge.AutoSave";

        struct Received { public int slot; public SfxPatch patch; public string time; public SfxPreset saved; }

        readonly List<Received> received = new List<Received>();
        readonly List<string> log = new List<string>();
        string[] inputs = new string[0], outputs = new string[0];
        string text = "";
        SfxSong lastSong;
        Vector2 scroll;
        int padMode;

        [MenuItem("Window/SFX Forge")]
        static void ShowWindow() => GetWindow<SfxForgeWindow>("SFX Forge").minSize = new Vector2(340, 420);

        void OnEnable()
        {
            RefreshPorts();
            SfxLink.Shared.PatchReceived += OnPatch;
            SfxLink.Shared.MessageReceived += OnMessage;
            SfxLink.Shared.SongReceived += OnSong;
        }

        void OnSong(byte[] song, SfxPatch[] sounds)
        {
            if (sounds == null) { Debug.LogWarning("SFX Forge: a song arrived without its 16 sounds – use 'Fetch the song' or SEND on the device's SONG page."); return; }
            lastSong = SfxForgeEditorLink.CreateSong(sounds, song, EditorPrefs.GetString(FolderPref, "Assets/SfxForge"), "Song");
            EditorGUIUtility.PingObject(lastSong);
            Repaint();
        }

        void OnDisable()
        {
            SfxLink.Shared.SongReceived -= OnSong;
            SfxLink.Shared.PatchReceived -= OnPatch;
            SfxLink.Shared.MessageReceived -= OnMessage;
        }

        void RefreshPorts() { inputs = MidiPorts.Inputs(); outputs = MidiPorts.Outputs(); }

        void OnPatch(int slot, SfxPatch patch)
        {
            var r = new Received { slot = slot, patch = patch, time = DateTime.Now.ToString("HH:mm:ss") };
            if (EditorPrefs.GetBool(AutoSavePref, true)) r.saved = Save(r);
            received.Insert(0, r);
            if (received.Count > 32) received.RemoveAt(received.Count - 1);
            Repaint();
        }

        void OnMessage(byte[] m)
        {
            log.Insert(0, SfxLink.Describe(m));
            if (log.Count > 6) log.RemoveAt(log.Count - 1);
            Repaint();
        }

        static SfxPreset Save(Received r)
        {
            var asset = SfxForgeEditorLink.CreatePreset(r.patch, EditorPrefs.GetString(FolderPref, "Assets/SfxForge"), $"Pad{r.slot + 1:00}_{r.patch.engine}");
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        static string PortPopup(string label, string[] names, string current)
        {
            var options = new string[names.Length + 1];
            options[0] = "(none)";
            Array.Copy(names, 0, options, 1, names.Length);
            int index = Mathf.Max(0, SfxLink.Find(names, current) + 1);
            int picked = EditorGUILayout.Popup(label, index, options);
            return picked == 0 ? "" : names[picked - 1];
        }

        void OnGUI()
        {
            var link = SfxLink.Shared;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);
            if (!MidiPorts.Supported)
                EditorGUILayout.HelpBox("MIDI is implemented for Windows only so far. Presets, SfxPlayer and baking do not need it.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(link.IsOpen))
            {
                SfxForgeEditorLink.InputPort = PortPopup("From device", inputs, SfxForgeEditorLink.InputPort);
                SfxForgeEditorLink.OutputPort = PortPopup("To device", outputs, SfxForgeEditorLink.OutputPort);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!link.IsOpen) { if (GUILayout.Button("Connect")) SfxForgeEditorLink.Connect(); }
                else if (GUILayout.Button("Disconnect")) SfxForgeEditorLink.Disconnect();
                if (GUILayout.Button("Refresh ports", GUILayout.Width(100))) RefreshPorts();
            }
            EditorGUILayout.LabelField("Status", link.IsOpen ? $"in: {link.InputName ?? "–"}   out: {link.OutputName ?? "–"}" : "not connected");
            if (inputs.Length + outputs.Length == 0 && MidiPorts.Supported)
                EditorGUILayout.HelpBox("No MIDI ports. Without the hardware, SFX FORGE runs in the browser or the plugin: install a virtual MIDI cable (e.g. loopMIDI) with two ports, give one to the browser's MIDI out and the other to its MIDI in.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Device", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!link.HasOutput))
            {
                padMode = GUILayout.Toolbar(padMode, new[] { "Pads play", "Pads fetch their patch" });
                for (int row = 0; row < 4; row++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                        for (int col = 0; col < 4; col++)
                        {
                            int slot = row * 4 + col;
                            if (!GUILayout.Button((slot + 1).ToString(), GUILayout.Height(30))) continue;
                            if (padMode == 0) { link.PlayPad(slot); link.ReleasePad(slot); }
                            else link.RequestPatch(slot);
                        }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    text = EditorGUILayout.TextField(text);
                    if (GUILayout.Button("Show on device", GUILayout.Width(110))) link.SendText(text);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Patches from the device", EditorStyles.boldLabel);
            EditorPrefs.SetBool(AutoSavePref, EditorGUILayout.ToggleLeft("Save as a preset as soon as one arrives (EXPORT on the device)", EditorPrefs.GetBool(AutoSavePref, true)));
            EditorPrefs.SetString(FolderPref, EditorGUILayout.TextField("Folder", EditorPrefs.GetString(FolderPref, "Assets/SfxForge")));
            if (received.Count == 0) EditorGUILayout.LabelField("Press EXPORT (F5) on the device, or fetch a pad above.", EditorStyles.miniLabel);
            for (int i = 0; i < received.Count; i++)
            {
                var r = received[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{r.time}  pad {r.slot + 1}  {r.patch.engine}");
                    if (GUILayout.Button("Play", GUILayout.Width(44))) SfxForgeEditorLink.Preview(r.patch);
                    if (r.saved != null) { if (GUILayout.Button("Show", GUILayout.Width(50))) EditorGUIUtility.PingObject(r.saved); }
                    else if (GUILayout.Button("Save", GUILayout.Width(50))) { r.saved = Save(r); received[i] = r; }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Music", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!link.HasOutput))
                    if (GUILayout.Button(new GUIContent("Fetch the song", "Asks the device for its 16 sounds and its song; they arrive as a Song asset. SEND on its SONG page does the same."))) link.RequestSong();
                if (lastSong != null && GUILayout.Button("Show the last one", GUILayout.Width(130))) EditorGUIUtility.PingObject(lastSong);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last messages in", EditorStyles.boldLabel);
            foreach (string line in log) EditorGUILayout.LabelField(line, EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }
    }
}
