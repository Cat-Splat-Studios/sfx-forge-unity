using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Builds the HUD's objects (DESIGN.md, "UI"): panels are Night Plum at 70 % with square corners and a Signal Cream
    /// line along the bottom edge only; text is Signal Cream with an Ink shadow, upper case. All positions are in HUD
    /// units (the view is 18 tall) relative to an anchor in a corner of the view, so the layout holds at any aspect.
    /// This class only builds; <see cref="HudView"/> keeps it up to date.
    /// </summary>
    public static class HudArt
    {
        const float PanelLineThickness = 0.07f;
        const float Margin = 0.5f;
        const float SmallText = 0.5f;
        const float NormalText = 0.7f;
        const float TitleText = 1.1f;
        const int ContentOrder = 1;
        const int TextOrder = 2;
        const float Left = 0f;
        const float Middle = 0.5f;
        const float Right = 1f;

        /// <summary>
        /// The bottom edge of the title card and the results panel, in HUD units above the middle of the view.
        /// </summary>
        const float CardBottom = 1f;

        /// <summary>The HUD, the title card and the results panel, drawn in front of the camera.</summary>
        public static GameObject Create(ArtKit kit, Camera camera, PlayerBase player, GameSessionBase session)
        {
            var hud = new GameObject("HUD");
            var view = hud.AddComponent<HudView>();
            view.Configure(camera, player, session);

            Transform playHud = Group("Play HUD", hud.transform);
            Transform topLeft = Group("Top Left", playHud);
            Transform topCenter = Group("Top Centre", playHud);
            Transform topRight = Group("Top Right", playHud);
            Transform bottomLeft = Group("Bottom Left", playHud);
            Transform titleCard = Group("Title Card", hud.transform);
            Transform resultsPanel = Group("Results Panel", hud.transform);
            view.ConfigureLayout(
                topLeft,
                topCenter,
                topRight,
                bottomLeft,
                playHud.gameObject,
                titleCard.gameObject,
                resultsPanel.gameObject
            );

            BuildPlayHud(kit, view, topLeft, topCenter, topRight, bottomLeft);
            BuildTitleCard(kit, titleCard);
            BuildResultsPanel(kit, view, resultsPanel);

            view.Arrange();
            view.ShowPhase(GamePhase.Title);
            return hud;
        }

        static void BuildPlayHud(
            ArtKit kit,
            HudView view,
            Transform topLeft,
            Transform topCenter,
            Transform topRight,
            Transform bottomLeft
        )
        {
            // Top-left: a Chime and how many are carried.
            Panel(kit, topLeft, new Rect(Margin, -1.8f, 3.9f, 1.3f));
            Transform icon = kit.CreatePart(
                "Chime Icon",
                kit.SharedMesh("Chime", ChimeArt.DiamondMesh),
                DepthLayer.Hud,
                ContentOrder,
                topLeft
            ).transform;
            icon.localPosition = new Vector3(1.25f, -1.15f, 0f);
            icon.localScale = Vector3.one * 1.6f;
            HudLabel chimes = Label(kit, "Chime Count", topLeft, new Vector2(2f, -1.5f), NormalText, Left);
            chimes.Show("000");

            // Top-centre: the time.
            Panel(kit, topCenter, new Rect(-2.7f, -1.8f, 5.4f, 1.3f));
            HudLabel time = Label(kit, "Time", topCenter, new Vector2(0f, -1.5f), NormalText, Middle);
            time.Show("0:00.00");

            // Top-right: the section's name, which fades away, so it gets no panel.
            HudLabel section = Label(kit, "Section Name", topRight, new Vector2(-Margin - 0.2f, -1.45f), 0.6f, Right);

            // Bottom-left: the speed meter.
            Transform meterPart = kit.CreatePart(
                "Speed Meter",
                null,
                DepthLayer.Hud,
                ContentOrder,
                bottomLeft,
                transparent: true
            ).transform;
            var meter = meterPart.gameObject.AddComponent<SpeedMeter>();
            Panel(kit, bottomLeft, new Rect(Margin, Margin, meter.Width + 0.6f, 1.5f));
            meterPart.localPosition = new Vector3(Margin + 0.3f, Margin + 0.28f, 0f);
            meter.Show(0f);

            view.ConfigurePlayHud(chimes, time, section, meter);
        }

        /// <summary>
        /// The title card. The camera keeps the character about 1.5 units below the middle of the view, its crest
        /// reaching to about -0.5: both full-screen cards sit in the upper half (<see cref="CardBottom"/>) so the
        /// character is never under a panel.
        /// </summary>
        static void BuildTitleCard(ArtKit kit, Transform titleCard)
        {
            Panel(kit, titleCard, new Rect(-11f, CardBottom, 22f, 4.6f));
            Label(kit, "Title", titleCard, new Vector2(0f, CardBottom + 2.5f), TitleText, Middle)
                .Show("BRIO: FIRST HARMONIC");
            Label(kit, "Prompt", titleCard, new Vector2(0f, CardBottom + 0.8f), NormalText, Middle).Show("PRESS JUMP");
        }

        static void BuildResultsPanel(ArtKit kit, HudView view, Transform resultsPanel)
        {
            // In the upper half like the title card, so the brake and the stop at the Resonator stay in view.
            Panel(kit, resultsPanel, new Rect(-8.5f, CardBottom, 17f, 6.4f));
            HudLabel chimes = Label(
                kit,
                "Result Chimes",
                resultsPanel,
                new Vector2(0f, CardBottom + 4.9f),
                NormalText,
                Middle
            );
            HudLabel time = Label(
                kit,
                "Result Time",
                resultsPanel,
                new Vector2(0f, CardBottom + 3.6f),
                NormalText,
                Middle
            );
            HudLabel topSpeed = Label(
                kit,
                "Result Top Speed",
                resultsPanel,
                new Vector2(0f, CardBottom + 2.3f),
                NormalText,
                Middle
            );
            Label(kit, "Prompt", resultsPanel, new Vector2(0f, CardBottom + 0.7f), SmallText, Middle)
                .Show("PRESS JUMP TO PLAY AGAIN");
            view.ConfigureResults(chimes, time, topSpeed);
        }

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A panel: the translucent fill and the cream line along its bottom edge.</summary>
        static void Panel(ArtKit kit, Transform parent, Rect area)
        {
            var builder = new MeshBuilder();
            builder.AddRect(area.min, area.max, Palette.HudPanel);
            builder.AddRect(area.min, new Vector2(area.xMax, area.yMin + PanelLineThickness), Palette.SignalCream);
            kit.CreatePart("Panel", builder.ToMesh("HUD Panel"), DepthLayer.Hud, 0, parent, transparent: true);
        }

        static HudLabel Label(
            ArtKit kit,
            string name,
            Transform parent,
            Vector2 position,
            float capHeight,
            float anchor
        )
        {
            GameObject part = kit.CreatePart(name, null, DepthLayer.Hud, TextOrder, parent, transparent: true);
            part.transform.localPosition = position;
            var label = part.AddComponent<HudLabel>();
            label.Configure(capHeight, anchor);
            return label;
        }
    }
}
