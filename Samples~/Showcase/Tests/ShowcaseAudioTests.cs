using System;
using System.Collections;
using CatSplat.SfxForge;
using NUnit.Framework;
using SfxShowcase.Audio;
using SfxShowcase.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// The sound of the showcase in Play mode: does SFX FORGE really sound, and does it follow the game the three ways
    /// the example shows (one-shots, held sounds bent while they play, adaptive music)? These tests wait for the audio
    /// thread, so they run in real time. This file and <c>Editor/ShowcaseAudio.cs</c> are the only ones outside
    /// <c>Scripts/Audio</c> that know the audio code.
    /// </summary>
    public sealed class ShowcaseAudioTests : ShowcaseTestBase
    {
        /// <summary>The demo song's breakdown, the pattern Static Field holds (the only section that holds one): the song's arrangement only gets there after eight bars.</summary>
        const int PatternE = 4;

        /// <summary>For how many bars the held pattern is watched after it was cued.</summary>
        const int BarsToHold = 3;

        static void IgnoreWithoutTheNativeLibrary()
        {
            if (!SfxNative.Available) Assert.Ignore("SFX FORGE's native library is not available here: there is no sound to check.");
        }

        // ---- Use 1: one-shots --------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AJumpIsHeardThroughSfxForge()
        {
            UseRealTime();
            IgnoreWithoutTheNativeLibrary();
            var effects = UnityEngine.Object.FindFirstObjectByType<SoundEffects>();
            var effectsSource = effects.GetComponent<AudioSource>();
            Assert.IsNull(effectsSource.clip, "the effects source has no clip of its own: nothing here is an audio file");
            Assert.IsTrue(effects.HasBakedClip(GameSound.Jump), "the jump was baked from its patch when the scene started");
            Assert.Less(Loudest(effectsSource), 0.0001f, "before the jump the effects are silent");

            yield return StartRun();
            input.JumpHeld = true;
            yield return UntilRealTime(() => events.Jumps > 0, 1f);
            Assert.AreEqual(1, events.Jumps);
            float heard = 0f;
            yield return UntilRealTime(() => { heard = Mathf.Max(heard, Loudest(effectsSource)); return heard > 0.01f; }, 1f);
            Assert.Greater(heard, 0.01f, "the baked SFX FORGE clip comes out of the effects AudioSource");

            // the live route: a Chime is synthesized by an SfxPlayer of the pool as it is collected
            SfxPlayer[] voices = effects.GetComponentsInChildren<SfxPlayer>();
            Assert.AreEqual(AudioInstaller.LiveVoiceCount, voices.Length, "the pool has its live voices");
            player.AddChimes(1);
            yield return UntilRealTime(() => Array.Exists(voices, voice => voice.IsPlaying), 1f);
            Assert.IsTrue(Array.Exists(voices, voice => voice.IsPlaying), "a collected Chime sounds on a live SfxPlayer");
        }

        // ---- Use 2: held sounds that follow the game ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWindUpRevAndTheRollHumFollowThePlayer()
        {
            UseRealTime();
            IgnoreWithoutTheNativeLibrary();
            var continuous = UnityEngine.Object.FindFirstObjectByType<ContinuousSounds>();
            Assert.IsNotNull(continuous, "the scene has its continuous sounds");
            SfxPlayer rev = HeldPlayer(continuous, "Wind-Up Rev");
            SfxPlayer hum = HeldPlayer(continuous, "Roll Hum");
            Assert.IsFalse(rev.IsPlaying || hum.IsPlaying, "standing still, neither sounds");

            yield return StartRun();
            input.Down = true;
            yield return UntilGameTime(() => false, 0.1f);
            yield return TapJump();
            Assert.IsTrue(player.IsCharging, "Down and Jump at rest winds up");
            yield return UntilRealTime(() => rev.IsPlaying, 1f);
            Assert.IsTrue(rev.IsPlaying, "the first tap starts the rev on its SfxPlayer");
            float pitchAfterOneTap = rev.pitchSemitones;

            for (int tap = 0; tap < 4; tap++) yield return TapJump();
            Assert.Greater(rev.pitchSemitones, pitchAfterOneTap + 1f, "more charge bends the rev's pitch upwards while it sounds");
            Assert.Greater(rev.fm, 0f, "and deepens its FM");

            input.Down = false;
            yield return UntilRealTime(() => events.Bursts > 0, 1f);
            Assert.AreEqual(1, events.Bursts, "letting go of Down releases the Burst");
            yield return UntilRealTime(() => hum.IsPlaying && hum.pitchSemitones > 0f, 1f);
            Assert.IsTrue(player.IsRolling, "the player rolls away");
            Assert.IsTrue(hum.IsPlaying, "rolling on the ground holds the hum on its SfxPlayer");
            Assert.Greater(hum.pitchSemitones, 0f, "bent upwards by the speed");
            Assert.Greater(hum.level, 0f, "and audible");

            // the roll ends at the first low step of the level: the hum is let go and fades over the patch's release
            yield return UntilRealTime(() => !player.IsRolling, 6f);
            Assert.IsFalse(player.IsRolling, "the roll ends");
            yield return UntilRealTime(() => !hum.IsPlaying && !rev.IsPlaying, 4f);
            Assert.IsFalse(hum.IsPlaying, "and the hum stops with it");
            Assert.IsFalse(rev.IsPlaying, "the rev stopped when the Burst was released");
        }

        static SfxPlayer HeldPlayer(ContinuousSounds continuous, string name)
        {
            Transform voice = continuous.transform.Find(name);
            Assert.IsNotNull(voice, $"the continuous sounds have a \"{name}\" voice");
            var sfxPlayer = voice.GetComponent<SfxPlayer>();
            Assert.IsNotNull(sfxPlayer.preset, $"\"{name}\" holds its preset");
            return sfxPlayer;
        }

        // ---- Use 3: adaptive music ---------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EnteringASectionCuesItsPatternAndTheMusicStaysOnIt()
        {
            UseRealTime();
            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            Assert.IsNotNull(music, "the scene has its music player");
            Assert.IsNotNull(music.GetComponent<MusicDirector>(), "and the music has its director");
            yield return StartRun();
            CollectionAssert.AreEqual(new[] { SectionId.SoftStart }, events.Sections, "the first section is announced when the run starts");

            // onto the landing flat of Static Field, five sections on: nothing walks or falls there
            SectionInfo staticField = Sections.Get(SectionId.StaticField);
            player.PlaceAt(new Vector2(staticField.StartX + 3f, level.Checkpoints[1].y + 1f));
            yield return UntilRealTime(() => events.Sections.Count > 1, 2f);
            CollectionAssert.AreEqual(new[] { SectionId.SoftStart, SectionId.StaticField }, events.Sections, "crossing into a section announces it, once");

            IgnoreWithoutTheNativeLibrary();
            yield return UntilRealTime(() => music.IsPlaying, 2f);
            Assert.IsTrue(music.IsPlaying, "the song runs");

            // The song's own arrangement (A B A B C D C D E …) reaches E only in its ninth bar, 16 s in: within one
            // bar it can only be the cue.
            float barSeconds = music.song.StepsPerPattern * 60f / music.song.Bpm / 4f;
            yield return UntilRealTime(() => music.Pattern == PatternE, barSeconds + 1f);
            Assert.AreEqual(PatternE, music.Pattern, "the music changes to the section's pattern (E) on the next bar line");

            // And it stays: the arrangement would leave E after two bars.
            int cuedInBar = music.Bar;
            bool leftThePattern = false;
            yield return UntilRealTime(() => { leftThePattern |= music.Pattern != PatternE; return music.Bar >= cuedInBar + BarsToHold; }, (BarsToHold + 1) * barSeconds);
            Assert.GreaterOrEqual(music.Bar, cuedInBar + BarsToHold, "the song played on");
            Assert.IsFalse(leftThePattern, $"and stayed on the section's pattern for {BarsToHold} more bars");
        }

        [UnityTest]
        public IEnumerator LoopPatternKeepsTheSongOnOnePatternAndLetsItGoAgain()
        {
            UseRealTime();
            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            Assert.IsNotNull(music, "the scene has its music player");
            IgnoreWithoutTheNativeLibrary();

            // a player of its own beside the game's: the same song, nothing cueing it
            var go = new GameObject("loop test", typeof(AudioSource), typeof(SfxMusicPlayer));
            try
            {
                go.GetComponent<AudioSource>().volume = 0f;
                var own = go.GetComponent<SfxMusicPlayer>();
                own.song = music.song;
                own.loopPattern = true;
                own.Reload();
                own.Play();
                yield return UntilRealTime(() => own.IsPlaying, 2f);
                Assert.IsTrue(own.IsPlaying, "the song runs");

                // The arrangement is A B A B …: without the loop it is on B in its second bar.
                float barSeconds = own.song.StepsPerPattern * 60f / own.song.Bpm / 4f;
                int first = own.Pattern;
                bool left = false;
                float until = Time.realtimeSinceStartup + 3.5f * barSeconds;
                yield return UntilRealTime(() => { left |= own.Pattern != first; return Time.realtimeSinceStartup >= until; }, 4f * barSeconds);
                Assert.IsFalse(left, "with loopPattern the song stays on its pattern for more than three bars");

                own.loopPattern = false;
                yield return UntilRealTime(() => own.Pattern != first, 2.5f * barSeconds);
                Assert.AreNotEqual(first, own.Pattern, "and without it the arrangement goes on");
            }
            finally { UnityEngine.Object.Destroy(go); }
        }

        [UnityTest]
        public IEnumerator TheMusicsIntensityFollowsSpeedAndDropsOnAHit()
        {
            UseRealTime();
            var director = UnityEngine.Object.FindFirstObjectByType<MusicDirector>();
            Assert.IsNotNull(director, "the scene has its music director");
            yield return StartRun();
            float atRest = director.Intensity;

            input.MoveX = 1f;
            yield return UntilRealTime(() => director.Intensity > atRest + 0.15f, 4f);
            Assert.Greater(director.Intensity, atRest + 0.15f, "running brings more of the band in: the intensity rises with speed");

            input.Release();
            player.AddChimes(3);
            Assert.IsTrue(player.Hurt(player.Position + Vector2.right), "a hit lands");
            yield return null;
            Assert.Less(director.Intensity, 0.1f, "the band flinches: only the kick is left right after a hit");
        }

        /// <summary>The largest sample an AudioSource is putting out right now.</summary>
        static float Loudest(AudioSource source)
        {
            var samples = new float[1024];
            float loudest = 0f;
            for (int channel = 0; channel < 2; channel++)
            {
                source.GetOutputData(samples, channel);
                foreach (float sample in samples) loudest = Mathf.Max(loudest, Mathf.Abs(sample));
            }
            return loudest;
        }
    }
}
