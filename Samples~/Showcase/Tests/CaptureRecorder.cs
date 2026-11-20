using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// Records the showcase to disk for a named "take": every frame as a 1920×1080 PNG, the game's sound as a wav that
    /// is sample-exact with those frames, and (through <see cref="CaptureTelemetry"/>) what the game and SFX FORGE were
    /// doing in each frame. Nobody holds a controller: a take is a Play mode test that scripts the input.
    ///
    /// How it stays in step:
    ///  • The clock is fixed at 30 frames a second (<c>Time.captureFramerate</c>), so a frame is always 1/30 s of game
    ///    time however long it takes to write.
    ///  • The picture is rendered by hand at the very end of the frame (this component runs after every other
    ///    LateUpdate), into a 16:9 render texture that is the camera's target for the WHOLE take. The camera's aspect
    ///    therefore is 16:9 for everything that lays itself out from it (the view's bounds, the backdrop, the HUD).
    ///  • The sound is not recorded from the sound card. <c>AudioRenderer</c> takes the audio system off the real-time
    ///    clock; each frame asks it for the samples that frame is due. Unity mixes in whole audio blocks (1024 samples
    ///    here), so a frame gets one or two blocks and the wav is never more than one block (21 ms) away from the
    ///    pictures: frame N ends within a block of sample (N + 1) × 1600 at 48 kHz. SFX FORGE synthesizes in
    ///    <c>OnAudioFilterRead</c>, which Unity then calls from inside <c>AudioRenderer.Render</c>, so sound made by an
    ///    event of frame N starts in the block rendered at the end of frame N.
    ///
    /// The game's own code is not changed and does not know it is being recorded.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class CaptureRecorder : MonoBehaviour
    {
        /// <summary>Width of every picture, pixels.</summary>
        public const int Width = 1920;
        /// <summary>Height of every picture, pixels.</summary>
        public const int Height = 1080;
        /// <summary>Pictures a second of game time.</summary>
        public const int FramesPerSecond = 30;

        /// <summary>
        /// The listener's volume while the rig is on. Unity limits its output to full scale before anything can read
        /// it, so a take recorded at 1 can never show that the game's mix went over: its largest sample would read
        /// exactly 1. A take is recorded with this much room instead, which makes the mix measurable: the game, played
        /// at 1, peaks at <see cref="Summary.Peak"/> ÷ this. Every take asserts that this stays under
        /// <see cref="MixCeiling"/>; whoever uses the wav brings the level back up (make_unity_videos.py does).
        /// </summary>
        public const float ListenerVolume = 0.5f;

        /// <summary>The most the game's mix may reach at full volume, of full scale (1 would be the edge of clipping).</summary>
        public const float MixCeiling = 0.95f;

        /// <summary>Samples of multisampling of the render target: the art is flat shapes with inked edges.</summary>
        const int Antialiasing = 4;
        /// <summary>Pictures waiting to be compressed before the game is made to wait for the disk.</summary>
        const int MostPicturesWaiting = 48;
        const int BytesPerPixel = 3;
        const int WavHeaderBytes = 44;
        const int WavChannels = 2;

        /// <summary>What a finished take consists of.</summary>
        public readonly struct Summary
        {
            /// <summary>The take's name.</summary>
            public readonly string Take;
            /// <summary>Pictures written.</summary>
            public readonly int Frames;
            /// <summary>Stereo sample frames written to the wav.</summary>
            public readonly long AudioFrames;
            /// <summary>The wav's sample rate, Hz.</summary>
            public readonly int SampleRate;
            /// <summary>The largest sample, 0..1 (above 1 the wav was clipped).</summary>
            public readonly float Peak;
            /// <summary>The root mean square of all samples, 0..1.</summary>
            public readonly float Rms;
            /// <summary>Samples that had to be clipped to fit 16 bits.</summary>
            public readonly long ClippedSamples;

            /// <summary>Creates a summary.</summary>
            public Summary(string take, int frames, long audioFrames, int sampleRate, float peak, float rms, long clippedSamples)
            {
                Take = take; Frames = frames; AudioFrames = audioFrames; SampleRate = sampleRate;
                Peak = peak; Rms = rms; ClippedSamples = clippedSamples;
            }

            /// <summary>The length of the pictures, seconds.</summary>
            public float PictureSeconds => Frames / (float)FramesPerSecond;
            /// <summary>The length of the wav, seconds.</summary>
            public float AudioSeconds => SampleRate > 0 ? AudioFrames / (float)SampleRate : 0f;
        }

        Camera viewCamera;
        string take, takeFolder, stillsFolder;
        RenderTexture target;
        Texture2D readback;
        bool rigged, recording, cameraWasEnabled;
        float listenerVolumeBefore;
        CaptureTelemetry telemetry;

        int frameCount;
        readonly List<string> stillsWanted = new List<string>();
        readonly ConcurrentBag<byte[]> spareBuffers = new ConcurrentBag<byte[]>();
        readonly List<Task> writes = new List<Task>();
        SemaphoreSlim waiting;
        volatile Exception writeError;

        FileStream wavFile;
        BinaryWriter wav;
        int sampleRate, outputChannels;
        long audioFrames, clippedSamples;
        float peak;
        double sumOfSquares;
        byte[] wavBlock = new byte[0];

        /// <summary>Pictures written so far: the index of the frame being played now.</summary>
        public int FrameCount => frameCount;
        /// <summary>The folder the take's files go to.</summary>
        public string TakeFolder => takeFolder;
        /// <summary>The wav's sample rate, Hz.</summary>
        public int SampleRate => sampleRate;
        /// <summary>Samples per channel in one frame's sound, on average.</summary>
        public int SamplesPerFrame => sampleRate / FramesPerSecond;

        /// <summary>
        /// Puts the rig on a camera: from now until <see cref="Finish"/> the clock is fixed at 30 frames a second and
        /// the camera looks into a 1920×1080 texture, so its aspect is 16:9 whatever the screen is, and the sound is
        /// rendered frame by frame instead of in real time, at <see cref="ListenerVolume"/>. Nothing is written yet. <paramref name="captureFolder"/> is the folder all takes share (out/unity-video).
        /// </summary>
        public static CaptureRecorder Create(Camera camera, string captureFolder, string takeName)
        {
            var host = new GameObject("Capture Recorder");
            var recorder = host.AddComponent<CaptureRecorder>();
            recorder.Rig(camera, captureFolder, takeName);
            return recorder;
        }

        void Rig(Camera camera, string captureFolder, string takeName)
        {
            viewCamera = camera;
            take = takeName;
            takeFolder = Path.Combine(captureFolder, takeName);
            stillsFolder = Path.Combine(captureFolder, "stills");
            Directory.CreateDirectory(takeFolder);
            Directory.CreateDirectory(stillsFolder);

            Time.captureDeltaTime = 0f;
            Time.captureFramerate = FramesPerSecond;

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = Antialiasing, name = "Capture Target" };
            target.Create();
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            // The camera's target for the whole take, not only while a picture is taken: Camera.aspect follows the
            // target, and the follow camera, the backdrop and the HUD read the aspect in their own LateUpdate.
            // Disabled, because this component renders it by hand once everything has moved.
            cameraWasEnabled = viewCamera.enabled;
            viewCamera.targetTexture = target;
            viewCamera.enabled = false;

            sampleRate = AudioSettings.outputSampleRate;
            outputChannels = ChannelsOf(AudioSettings.speakerMode);
            if (sampleRate % FramesPerSecond != 0) Debug.LogWarning($"CAPTURE: {sampleRate} Hz is not a whole number of samples per frame; AudioRenderer spreads the remainder.");
            listenerVolumeBefore = AudioListener.volume;
            AudioListener.volume = ListenerVolume;
            if (!AudioRenderer.Start()) throw new InvalidOperationException("AudioRenderer.Start failed: the sound cannot be rendered offline here.");
            rigged = true;
        }

        /// <summary>
        /// Starts writing: from the frame this is called in, every frame becomes a picture and one frame of sound, and
        /// <paramref name="frameTelemetry"/> (may be null) is asked to note the frame. Old files of the take are removed.
        /// </summary>
        public void BeginRecording(CaptureTelemetry frameTelemetry)
        {
            if (!rigged || recording) throw new InvalidOperationException("BeginRecording needs a rigged recorder that is not recording yet.");
            // also the old telemetry: a take that fails part-way must not leave new pictures beside old telemetry
            foreach (string old in Directory.GetFiles(takeFolder, "f*.png")) File.Delete(old);
            File.Delete(Path.Combine(takeFolder, "telemetry.json"));

            telemetry = frameTelemetry;
            waiting = new SemaphoreSlim(MostPicturesWaiting, MostPicturesWaiting);
            wavFile = new FileStream(Path.Combine(takeFolder, "audio.wav"), FileMode.Create, FileAccess.Write);
            wav = new BinaryWriter(wavFile);
            wav.Write(new byte[WavHeaderBytes]);                       // filled in by Finish, when the length is known
            frameCount = 0;
            recording = true;
        }

        /// <summary>
        /// Saves the picture of the frame being played now as stills/&lt;name&gt;.png as well. Works with or without
        /// recording.
        /// </summary>
        public void RequestStill(string name)
        {
            if (!rigged) throw new InvalidOperationException("RequestStill needs a rigged recorder.");
            stillsWanted.Add(name);
        }

        void LateUpdate()
        {
            if (!rigged) return;
            bool wantsPicture = recording || stillsWanted.Count > 0;
            if (wantsPicture) TakePicture();

            foreach (string still in stillsWanted)
                File.WriteAllBytes(Path.Combine(stillsFolder, still + ".png"), readback.EncodeToPNG());
            stillsWanted.Clear();

            // The sound is rendered in every frame from the moment the rig is on, and only written while recording:
            // what happens before a take rolls (a cut, waiting for a bar line) is then on the frame clock as well.
            RenderSound();
            if (!recording) return;
            WritePicture(frameCount);
            telemetry?.NoteFrame(frameCount, audioFrames);
            frameCount++;
            if (writeError != null) throw new IOException("A picture could not be written.", writeError);
        }

        /// <summary>Renders the camera into the target and reads the pixels back.</summary>
        void TakePicture()
        {
            viewCamera.Render();
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = before;
        }

        /// <summary>Hands the pixels to a worker thread, which compresses and writes them; waits if too many are queued.</summary>
        void WritePicture(int index)
        {
            if (!spareBuffers.TryTake(out byte[] pixels)) pixels = new byte[Width * Height * BytesPerPixel];
            readback.GetRawTextureData<byte>().CopyTo(pixels);
            string path = Path.Combine(takeFolder, "f" + index.ToString("00000", CultureInfo.InvariantCulture) + ".png");

            waiting.Wait();
            SemaphoreSlim gate = waiting;
            writes.Add(Task.Run(() =>
            {
                try
                {
                    byte[] png = ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8_UNorm, Width, Height);
                    File.WriteAllBytes(path, png);
                }
                catch (Exception problem) { writeError = problem; }
                finally
                {
                    spareBuffers.Add(pixels);
                    gate.Release();
                }
            }));
        }

        /// <summary>Renders the sound this frame is due (whole audio blocks) and appends it to the wav as 16-bit stereo.</summary>
        void RenderSound()
        {
            int frames = AudioRenderer.GetSampleCountForCaptureFrame();
            var samples = new NativeArray<float>(frames * outputChannels, Allocator.Temp);
            try
            {
                if (!AudioRenderer.Render(samples)) throw new InvalidOperationException("AudioRenderer.Render failed.");
                if (!recording) return;
                if (wavBlock.Length < frames * WavChannels * 2) wavBlock = new byte[frames * WavChannels * 2];
                int at = 0;
                for (int frame = 0; frame < frames; frame++)
                {
                    float left = samples[frame * outputChannels];
                    float right = outputChannels > 1 ? samples[frame * outputChannels + 1] : left;
                    at = Put(left, at);
                    at = Put(right, at);
                }
                wav.Write(wavBlock, 0, at);
                audioFrames += frames;
            }
            finally { samples.Dispose(); }
        }

        int Put(float sample, int at)
        {
            float size = Mathf.Abs(sample);
            if (size > peak) peak = size;
            sumOfSquares += (double)sample * sample;
            if (size > 1f) { clippedSamples++; sample = Mathf.Sign(sample); }
            short value = (short)Mathf.RoundToInt(sample * short.MaxValue);
            wavBlock[at] = (byte)(value & 0xFF);
            wavBlock[at + 1] = (byte)((value >> 8) & 0xFF);
            return at + 2;
        }

        /// <summary>
        /// Stops, waits for every picture to be on disk, closes the wav, gives the camera and the clock back and
        /// removes the recorder. Returns what was written and logs it as one "CAPTURE" line.
        /// </summary>
        public Summary Finish()
        {
            bool wasRecording = recording;
            Unrig();
            float rms = audioFrames > 0 ? (float)Math.Sqrt(sumOfSquares / (audioFrames * WavChannels)) : 0f;
            var summary = new Summary(take, frameCount, audioFrames, sampleRate, peak, rms, clippedSamples);
            if (wasRecording)
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "CAPTURE take={0} frames={1} seconds={2:0.000} wavSeconds={3:0.000} sampleRate={4} peak={5:0.0000} rms={6:0.0000} clipped={7} folder={8}",
                    take, summary.Frames, summary.PictureSeconds, summary.AudioSeconds, sampleRate, peak, rms, clippedSamples, takeFolder));
            if (writeError != null) throw new IOException("A picture could not be written.", writeError);
            Destroy(gameObject);
            return summary;
        }

        void OnDestroy() => Unrig();

        /// <summary>Everything Finish undoes, safe to call twice (a failed take is cleaned up by OnDestroy).</summary>
        void Unrig()
        {
            if (!rigged) return;
            rigged = false;
            AudioRenderer.Stop();
            AudioListener.volume = listenerVolumeBefore;

            if (recording)
            {
                recording = false;
                Task.WaitAll(writes.ToArray());
                writes.Clear();
                WriteWavHeader();
                wav.Dispose();
                wavFile.Dispose();
            }

            Time.captureFramerate = 0;
            if (viewCamera != null)
            {
                viewCamera.targetTexture = null;
                viewCamera.enabled = cameraWasEnabled;
            }
            if (target != null) { target.Release(); Destroy(target); }
            if (readback != null) Destroy(readback);
        }

        void WriteWavHeader()
        {
            const int bitsPerSample = 16;
            int dataBytes = (int)(audioFrames * WavChannels * (bitsPerSample / 8));
            wav.Flush();
            wavFile.Seek(0, SeekOrigin.Begin);
            wav.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            wav.Write(WavHeaderBytes - 8 + dataBytes);
            wav.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            wav.Write(16);                                             // the size of the format block
            wav.Write((short)1);                                       // PCM
            wav.Write((short)WavChannels);
            wav.Write(sampleRate);
            wav.Write(sampleRate * WavChannels * (bitsPerSample / 8));
            wav.Write((short)(WavChannels * (bitsPerSample / 8)));
            wav.Write((short)bitsPerSample);
            wav.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            wav.Write(dataBytes);
            wav.Flush();
        }

        static int ChannelsOf(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 2;
            }
        }
    }
}
