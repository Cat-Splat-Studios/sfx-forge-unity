using System.Globalization;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The HUD, the title card and the results panel. It only READS the session and the player and shows what the
    /// session's phase asks for: the title card on <see cref="GamePhase.Title"/>, the play HUD (Chimes, time, speed
    /// meter, section name) on <see cref="GamePhase.Playing"/>, the results on <see cref="GamePhase.Finished"/>.
    /// It is drawn with meshes just in front of the camera, in "HUD units": the view is always 18 units tall
    /// (<see cref="ArtStyle.ViewHalfHeight"/> × 2) whatever the camera's size, and the corners are found from the
    /// camera's aspect every frame. Runs after the follow camera has moved.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class HudView : MonoBehaviour
    {
        [Header("Wiring (set by HudArt)")]
        [SerializeField, Tooltip("The game camera the HUD is drawn in front of.")]
        Camera viewCamera;

        [SerializeField, Tooltip("The player, for the speed meter.")]
        PlayerBase player;

        [SerializeField, Tooltip("The run, for the phase, Chimes, time and section.")]
        GameSessionBase session;

        [SerializeField]
        Transform topLeft;

        [SerializeField]
        Transform topCenter;

        [SerializeField]
        Transform topRight;

        [SerializeField]
        Transform bottomLeft;

        [SerializeField]
        GameObject playHud;

        [SerializeField]
        GameObject titleCard;

        [SerializeField]
        GameObject resultsPanel;

        [SerializeField]
        HudLabel chimeLabel;

        [SerializeField]
        HudLabel timeLabel;

        [SerializeField]
        HudLabel sectionLabel;

        [SerializeField]
        SpeedMeter speedMeter;

        [SerializeField]
        HudLabel resultChimes;

        [SerializeField]
        HudLabel resultTime;

        [SerializeField]
        HudLabel resultTopSpeed;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Distance in front of the camera's near plane, metres.")]
        float distanceFromNearPlane = 1f;

        [SerializeField, Tooltip("Seconds a section's name stays on screen after entering the section.")]
        float sectionNameSeconds = 3f;

        [SerializeField, Tooltip("Seconds the section's name takes to fade out after that.")]
        float sectionFadeSeconds = 0.5f;

        GamePhase shownPhase = GamePhase.Title;
        bool anySectionShown;
        SectionId shownSection;
        float sectionShownAt;

        /// <summary>Wires what the HUD reads. Called by <see cref="HudArt"/>.</summary>
        public void Configure(Camera followedCamera, PlayerBase shownPlayer, GameSessionBase shownSession)
        {
            viewCamera = followedCamera;
            player = shownPlayer;
            session = shownSession;
        }

        /// <summary>Wires the four corner anchors and the three screens. Called by <see cref="HudArt"/>.</summary>
        public void ConfigureLayout(
            Transform topLeftAnchor,
            Transform topCenterAnchor,
            Transform topRightAnchor,
            Transform bottomLeftAnchor,
            GameObject playHudGroup,
            GameObject titleCardGroup,
            GameObject resultsPanelGroup
        )
        {
            topLeft = topLeftAnchor;
            topCenter = topCenterAnchor;
            topRight = topRightAnchor;
            bottomLeft = bottomLeftAnchor;
            playHud = playHudGroup;
            titleCard = titleCardGroup;
            resultsPanel = resultsPanelGroup;
        }

        /// <summary>Wires the parts of the play HUD. Called by <see cref="HudArt"/>.</summary>
        public void ConfigurePlayHud(HudLabel chimes, HudLabel time, HudLabel section, SpeedMeter meter)
        {
            chimeLabel = chimes;
            timeLabel = time;
            sectionLabel = section;
            speedMeter = meter;
        }

        /// <summary>Wires the lines of the results panel. Called by <see cref="HudArt"/>.</summary>
        public void ConfigureResults(HudLabel chimes, HudLabel time, HudLabel topSpeed)
        {
            resultChimes = chimes;
            resultTime = time;
            resultTopSpeed = topSpeed;
        }

        /// <summary>
        /// Puts the HUD in front of the camera and its anchors in the corners of the view. Called every frame, and once
        /// when the scene is built.
        /// </summary>
        public void Arrange()
        {
            Transform view = viewCamera.transform;
            transform.SetPositionAndRotation(
                view.position + view.forward * (viewCamera.nearClipPlane + distanceFromNearPlane),
                view.rotation
            );
            transform.localScale = Vector3.one * (viewCamera.orthographicSize / ArtStyle.ViewHalfHeight);

            float halfHeight = ArtStyle.ViewHalfHeight;
            float halfWidth = halfHeight * viewCamera.aspect;
            topLeft.localPosition = new Vector3(-halfWidth, halfHeight, 0f);
            topCenter.localPosition = new Vector3(0f, halfHeight, 0f);
            topRight.localPosition = new Vector3(halfWidth, halfHeight, 0f);
            bottomLeft.localPosition = new Vector3(-halfWidth, -halfHeight, 0f);
        }

        /// <summary>Shows one of the three screens and hides the other two.</summary>
        public void ShowPhase(GamePhase phase)
        {
            titleCard.SetActive(phase == GamePhase.Title);
            playHud.SetActive(phase == GamePhase.Playing);
            resultsPanel.SetActive(phase == GamePhase.Finished);
        }

        void Start()
        {
            ShowPhase(session.Phase);
            shownPhase = session.Phase;
        }

        void LateUpdate()
        {
            Arrange();

            GamePhase phase = session.Phase;
            if (phase != shownPhase)
            {
                shownPhase = phase;
                ShowPhase(phase);
                if (phase == GamePhase.Playing)
                {
                    anySectionShown = false;
                }

                if (phase == GamePhase.Finished)
                {
                    FillResults();
                }
            }

            if (phase == GamePhase.Playing)
            {
                UpdatePlayHud();
            }
        }

        void UpdatePlayHud()
        {
            chimeLabel.Show(session.ChimeCount.ToString("000", CultureInfo.InvariantCulture));
            timeLabel.Show(FormatTime(session.ElapsedSeconds));
            speedMeter.Show(player.Speed01);

            SectionId section = session.CurrentSection;
            if (!anySectionShown || section != shownSection)
            {
                anySectionShown = true;
                shownSection = section;
                sectionShownAt = Time.time;
            }

            float fade = (Time.time - sectionShownAt - sectionNameSeconds) / sectionFadeSeconds;
            sectionLabel.Show(Sections.Get(section).DisplayName, 1f - Mathf.Clamp01(fade));
        }

        void FillResults()
        {
            resultChimes.Show("CHIMES " + session.ChimeCount.ToString("000", CultureInfo.InvariantCulture));
            resultTime.Show("TIME " + FormatTime(session.ElapsedSeconds));
            resultTopSpeed.Show(
                "TOP SPEED " + session.TopSpeedReached.ToString("0.0", CultureInfo.InvariantCulture) + " M/S"
            );
        }

        /// <summary>Seconds as m:ss.cc (minutes, seconds, hundredths).</summary>
        static string FormatTime(float seconds)
        {
            int hundredths = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 100f);
            int minutes = hundredths / 6000;
            int wholeSeconds = hundredths / 100 % 60;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:00}.{2:00}",
                minutes,
                wholeSeconds,
                hundredths % 100
            );
        }
    }
}
