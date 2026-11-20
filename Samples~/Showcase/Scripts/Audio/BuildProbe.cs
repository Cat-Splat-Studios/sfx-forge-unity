using System;
using System.Globalization;
using System.IO;
using System.Text;
using CatSplat.SfxForge;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// THE BUILD PROVES ITSELF. A built player started with
    ///
    ///     Brio.exe -sfxforge-probe C:\somewhere\probe.json [-sfxforge-probe-seconds 14]
    ///
    /// plays by itself for a few seconds (Jump on the title card, then right with a jump now and then), listens to what
    /// actually comes out of the AudioListener, and writes down what it found: is the native library there, does the
    /// song run and move through its bars, were clips baked, did sound effects fire, how loud was the output. Then it
    /// writes a picture beside the report and quits with exit code 0 (everything it looks for was there) or 1.
    /// Without the argument this class does nothing at all: it is not in the scene, it makes itself on start.
    ///
    /// This is what `ShowcaseBuild.BuildAndProbe` runs after building (README, "A standalone build").
    /// </summary>
    public sealed class BuildProbe : MonoBehaviour, IPlayerInput
    {
        const string Argument = "-sfxforge-probe", SecondsArgument = "-sfxforge-probe-seconds";
        const float DefaultSeconds = 14f, TitleSeconds = 2f, JumpEverySeconds = 1.6f, JumpHoldSeconds = 0.35f;
        const float Audible = 0.003f;                       // an output RMS above this is sound, not a silent device
        const int OutputSamples = 1024;

        string reportPath;
        float seconds;
        PlayerBase player;
        GameSessionBase session;
        SfxMusicPlayer music;
        MusicDirector director;
        readonly float[] output = new float[OutputSamples];

        // what was seen
        float startedAt, peak, rmsSum, loudestRms, highestIntensity, farthestX;
        int rmsCount, audibleFrames, frames, firstBar = -1, lastBar = -1, barChanges;
        int jumps, landings, footsteps, chimes, sections;
        bool sawPlaying, songRan, pictureTaken;
        readonly StringBuilder patterns = new StringBuilder();
        int lastPattern = -1;

        /// <inheritdoc/>
        public float MoveX { get; private set; }
        /// <inheritdoc/>
        public bool Down => false;
        /// <inheritdoc/>
        public bool JumpHeld { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MakeItselfIfAsked()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(arguments, Argument);
            if (at < 0 || at + 1 >= arguments.Length) return;
            var probe = new GameObject("Build Probe").AddComponent<BuildProbe>();
            probe.reportPath = Path.GetFullPath(arguments[at + 1]);
            int secondsAt = Array.IndexOf(arguments, SecondsArgument);
            probe.seconds = secondsAt >= 0 && secondsAt + 1 < arguments.Length
                && float.TryParse(arguments[secondsAt + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float asked) ? asked : DefaultSeconds;
            DontDestroyOnLoad(probe.gameObject);
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerBase>();
            session = FindFirstObjectByType<GameSessionBase>();
            music = FindFirstObjectByType<SfxMusicPlayer>();
            director = FindFirstObjectByType<MusicDirector>();
            if (player != null) player.InputOverride = this;
            startedAt = Time.realtimeSinceStartup;
            Application.runInBackground = true;             // the probe must not stop when its window is not in front
            GameEvents.Jumped += OnJumped;
            GameEvents.Landed += OnLanded;
            GameEvents.Footstep += OnFootstep;
            GameEvents.ChimeCollected += OnChime;
            GameEvents.SectionEntered += OnSection;
        }

        void OnDestroy()
        {
            GameEvents.Jumped -= OnJumped;
            GameEvents.Landed -= OnLanded;
            GameEvents.Footstep -= OnFootstep;
            GameEvents.ChimeCollected -= OnChime;
            GameEvents.SectionEntered -= OnSection;
        }

        void OnJumped() => jumps++;
        void OnLanded(float speed) => landings++;
        void OnFootstep(float speed01) => footsteps++;
        void OnChime(int combo) => chimes++;
        void OnSection(SectionId section) => sections++;

        void Update()
        {
            float t = Time.realtimeSinceStartup - startedAt;
            frames++;

            // the controls: Jump on the title card, then right, with a jump every now and then
            bool playing = session != null && session.Phase == GamePhase.Playing;
            sawPlaying |= playing;
            if (!playing) { MoveX = 0f; JumpHeld = t > TitleSeconds && Mathf.Repeat(t, 0.5f) < 0.25f; }
            else { MoveX = 1f; JumpHeld = Mathf.Repeat(t, JumpEverySeconds) < JumpHoldSeconds; }

            // what comes out: the listener's own output, after every source and the engine's mix
            AudioListener.GetOutputData(output, 0);
            float sum = 0f;
            foreach (float sample in output) { sum += sample * sample; peak = Mathf.Max(peak, Mathf.Abs(sample)); }
            float rms = Mathf.Sqrt(sum / output.Length);
            rmsSum += rms; rmsCount++;
            loudestRms = Mathf.Max(loudestRms, rms);
            if (rms > Audible) audibleFrames++;

            if (music != null && music.IsPlaying)
            {
                songRan = true;
                int bar = music.Bar;
                if (firstBar < 0) firstBar = bar;
                if (bar != lastBar) { if (lastBar >= 0) barChanges++; lastBar = bar; }
                if (music.Pattern != lastPattern && patterns.Length < 64) { lastPattern = music.Pattern; patterns.Append((char)('A' + lastPattern)); }
            }
            if (director != null) highestIntensity = Mathf.Max(highestIntensity, director.Intensity);
            if (player != null) farthestX = Mathf.Max(farthestX, player.Position.x);

            if (!pictureTaken && t > seconds * 0.6f)
            {
                pictureTaken = true;
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(reportPath, ".png"));
            }
            if (t >= seconds) Finish();
        }

        void Finish()
        {
            enabled = false;
            int baked = 0, bakedWanted = 0;
            var effects = FindFirstObjectByType<SoundEffects>();
            foreach (SoundDefinition definition in SoundLibrary.All)
            {
                if (definition.Route != SoundRoute.Baked) continue;
                bakedWanted++;
                if (effects != null && effects.HasBakedClip(definition.Id)) baked++;
            }

            bool native = SfxNative.Available;
            bool heard = audibleFrames > rmsCount / 4;       // sound for at least a quarter of the frames
            bool ok = native && sawPlaying && songRan && barChanges >= 2 && baked == bakedWanted && heard && jumps > 0;

            var json = new StringBuilder("{\n");
            void Line(string name, string value, bool last = false) => json.Append("  \"").Append(name).Append("\": ").Append(value).Append(last ? "\n" : ",\n");
            string Number(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
            string Truth(bool value) => value ? "true" : "false";
            Line("ok", Truth(ok));
            Line("unity", "\"" + Application.unityVersion + "\"");
            Line("platform", "\"" + Application.platform + "\"");
            Line("isEditor", Truth(Application.isEditor));
            Line("seconds", Number(Time.realtimeSinceStartup - startedAt));
            Line("frames", frames.ToString(CultureInfo.InvariantCulture));
            Line("nativeLibrary", Truth(native));
            Line("leftTheTitleCard", Truth(sawPlaying));
            Line("songRan", Truth(songRan));
            Line("barsPlayed", barChanges.ToString(CultureInfo.InvariantCulture));
            Line("patternsHeard", "\"" + patterns + "\"");
            Line("highestIntensity", Number(highestIntensity));
            Line("bakedClips", baked.ToString(CultureInfo.InvariantCulture));
            Line("bakedClipsWanted", bakedWanted.ToString(CultureInfo.InvariantCulture));
            Line("outputSampleRate", AudioSettings.outputSampleRate.ToString(CultureInfo.InvariantCulture));
            Line("outputPeak", Number(peak));
            Line("outputRmsMean", Number(rmsCount > 0 ? rmsSum / rmsCount : 0f));
            Line("outputRmsLoudest", Number(loudestRms));
            Line("framesWithSound", audibleFrames.ToString(CultureInfo.InvariantCulture));
            Line("jumps", jumps.ToString(CultureInfo.InvariantCulture));
            Line("landings", landings.ToString(CultureInfo.InvariantCulture));
            Line("footsteps", footsteps.ToString(CultureInfo.InvariantCulture));
            Line("chimes", chimes.ToString(CultureInfo.InvariantCulture));
            Line("sectionsEntered", sections.ToString(CultureInfo.InvariantCulture));
            Line("farthestX", Number(farthestX), last: true);
            json.Append("}\n");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, json.ToString());
            }
            catch (Exception e) { Debug.LogError("BuildProbe could not write " + reportPath + ": " + e.Message); ok = false; }
            Debug.Log("BUILD PROBE " + (ok ? "ok" : "FAILED") + "\n" + json);
            Application.Quit(ok ? 0 : 1);
        }
    }
}
