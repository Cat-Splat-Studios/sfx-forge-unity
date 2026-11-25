// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// Window > SFX Forge: the MIDI ports, the device's 16 pads, and the patches it sends – as assets.
    /// It looks as the device's display does (SfxForgeView); this class is the link's side of it: it fills the view's
    /// model from SfxLink and does what the view asks for.
    /// </summary>
    public class SfxForgeWindow : EditorWindow
    {
        const string FolderPref = "SfxForge.Folder";
        const string AutoSavePref = "SfxForge.AutoSave";

        readonly SfxForgeModel model = new SfxForgeModel();
        SfxForgeView view;
        SfxSong lastSong;
        bool changed;

        [MenuItem("Window/SFX Forge")]
        static void ShowWindow() => GetWindow<SfxForgeWindow>("SFX Forge").minSize = new Vector2(340, 420);

        /// <summary>
        /// The whole window as one element: what CreateGUI shows, and what a test or a picture can
        /// build from any model.
        /// </summary>
        public static VisualElement Build(SfxForgeModel model) => new SfxForgeView(model);

        void OnEnable()
        {
            var link = SfxLink.Shared;
            link.PatchReceived += OnPatch;
            link.MessageReceived += OnMessage;
            link.SongReceived += OnSong;
            link.EncoderChanged += OnKnob;
            link.PadDown += OnPadDown;
            link.PadUp += OnPadUp;
            link.NewerReceived += OnNewer;

            model.midiSupported = MidiPorts.Supported;
            model.autoSave = EditorPrefs.GetBool(AutoSavePref, true);
            model.folder = EditorPrefs.GetString(FolderPref, SfxForgeModel.DefaultFolder);
            model.inputPort = SfxForgeEditorLink.InputPort;
            model.outputPort = SfxForgeEditorLink.OutputPort;
            model.connect = () =>
            {
                SfxForgeEditorLink.Connect();
                Sync();
            };
            model.disconnect = () =>
            {
                SfxForgeEditorLink.Disconnect();
                Sync();
            };
            model.refreshPorts = RefreshPorts;
            model.pickInput = name =>
            {
                model.inputPort = name;
                SfxForgeEditorLink.InputPort = name;
                changed = true;
            };
            model.pickOutput = name =>
            {
                model.outputPort = name;
                SfxForgeEditorLink.OutputPort = name;
                changed = true;
            };
            model.playPad = pad => link.PlayPad(pad);
            model.releasePad = pad => link.ReleasePad(pad);
            model.fetchPad = pad => link.RequestPatch(pad);
            model.sendText = text => link.SendText(text);
            model.setAutoSave = on =>
            {
                EditorPrefs.SetBool(AutoSavePref, model.autoSave = on);
                changed = true;
            };
            model.setFolder = folder =>
            {
                EditorPrefs.SetString(FolderPref, model.folder = folder);
                changed = true;
            };
            model.play = p => SfxForgeEditorLink.Preview(p.patch);
            model.save = p =>
            {
                p.saved = Save(p);
                changed = true;
            };
            model.show = p =>
            {
                if (p.saved != null)
                {
                    EditorGUIUtility.PingObject(p.saved);
                }
            };
            model.fetchSong = () => link.RequestSong();
            model.showSong = () =>
            {
                if (lastSong != null)
                {
                    EditorGUIUtility.PingObject(lastSong);
                }
            };
            RefreshPorts();
        }

        void OnDisable()
        {
            var link = SfxLink.Shared;

            // (a pad held with the mouse must not stay held on the device)
            if (view != null)
            {
                view.LetGo();
            }

            link.NewerReceived -= OnNewer;
            link.PadUp -= OnPadUp;
            link.PadDown -= OnPadDown;
            link.EncoderChanged -= OnKnob;
            link.SongReceived -= OnSong;
            link.PatchReceived -= OnPatch;
            link.MessageReceived -= OnMessage;
        }

        void CreateGUI()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.contentContainer.style.paddingLeft = 6;
            scroll.contentContainer.style.paddingRight = 6;
            view = (SfxForgeView)Build(model);
            scroll.Add(view);
            rootVisualElement.Add(scroll);

            // the link is polled by SfxForgeEditorLink; here only what it
            // is now is looked at, and what changed is shown
            rootVisualElement.schedule.Execute(Tick).Every(100);
        }

        void Tick()
        {
            Sync();
            if (!changed || view == null)
            {
                return;
            }

            changed = false;
            view.Refresh();
        }

        // the link as it is now (it can be opened and closed from elsewhere: a SfxForgeLink in a running game)
        void Sync()
        {
            var link = SfxLink.Shared;
            bool song = lastSong != null;

            // what the device said it is; or, a second after it was asked, that it did not say
            var device = link.Device;
            string identity =
                device.HasValue ? device.Value.ToString()
                : link.IdentityUnanswered ? SfxForgeModel.NoIdentity
                : null;
            if (model.identity != identity)
            {
                model.identity = identity;
                model.identityKnown = device.HasValue;
                changed = true;
            }

            if (
                model.isOpen == link.IsOpen
                && model.hasOutput == link.HasOutput
                && model.inputName == link.InputName
                && model.outputName == link.OutputName
                && model.hasSong == song
            )
            {
                return;
            }

            if (model.isOpen && !link.IsOpen)
            {
                model.ForgetKnobs();
                Array.Clear(model.padDown, 0, model.padDown.Length);
            }

            model.isOpen = link.IsOpen;
            model.hasOutput = link.HasOutput;
            model.inputName = link.InputName;
            model.outputName = link.OutputName;
            model.hasSong = song;
            changed = true;
        }

        void RefreshPorts()
        {
            model.inputs = MidiPorts.Inputs();
            model.outputs = MidiPorts.Outputs();
            changed = true;
        }

        void OnPatch(int slot, SfxPatch patch)
        {
            var p = model.TakePatch(slot, patch, DateTime.Now.ToString("HH:mm:ss"));
            if (model.autoSave)
            {
                p.saved = Save(p);
            }

            changed = true;
        }

        void OnMessage(byte[] m)
        {
            model.TakeMessage(SfxLink.Describe(m));
            changed = true;
        }

        // a patch or a song in a format above this version's came in: it is not an asset here, and the user is told why
        void OnNewer(string what)
        {
            Debug.LogWarning("SFX Forge: " + what);
            model.TakeMessage("NEWER: " + what);
            changed = true;
        }

        void OnKnob(int index, float value)
        {
            model.TakeKnob(index, value);
            changed = true;
        }

        void OnPadDown(int pad, float velocity)
        {
            model.TakePad(pad, true);
            changed = true;
        }

        void OnPadUp(int pad)
        {
            model.TakePad(pad, false);
            changed = true;
        }

        void OnSong(byte[] song, SfxPatch[] sounds)
        {
            if (sounds == null)
            {
                Debug.LogWarning(
                    "SFX Forge: a song arrived without its 16 sounds – use FETCH SONG or SEND on the device's SONG "
                        + "page."
                );
                return;
            }

            lastSong = SfxForgeEditorLink.CreateSong(sounds, song, model.folder, "Song");
            EditorGUIUtility.PingObject(lastSong);
            changed = true;
        }

        SfxPreset Save(SfxForgeModel.Patch p)
        {
            var asset = SfxForgeEditorLink.CreatePreset(p.patch, model.folder, $"Pad{p.slot + 1:00}_{p.patch.engine}");
            EditorGUIUtility.PingObject(asset);
            return asset;
        }
    }
}
