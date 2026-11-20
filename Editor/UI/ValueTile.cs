// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// The display's dial: a ring that stands where the knob stands, the encoder's number inside (dial() in the firmware).
    /// Only a picture – ValueTile is the control.
    /// </summary>
    public class Knob : VisualElement
    {
        readonly TextElement number;
        float bar = -1f;
        bool centre, unlit;
        Color accent = SfxColors.Ember;

        public Knob()
        {
            AddToClassList("sfx-knob");
            number = SfxUI.Text("", "sfx-knob__number");
            Add(number);
            generateVisualContent += Draw;
        }

        /// <summary>The encoder's number, "1".."4" – or nothing.</summary>
        public string Number { get => number.text; set => number.text = value; }

        /// <summary>bar: 0..1 where the knob stands; below 0: no value (a ring only). centre: it grows from the top, both ways.</summary>
        public void Set(float newBar, bool fromCentre, Color colour)
        {
            if (Mathf.Approximately(newBar, bar) && fromCentre == centre && colour == accent) return;
            bar = newBar; centre = fromCentre; accent = colour;
            MarkDirtyRepaint();
        }

        /// <summary>True: the dial of something that cannot be turned here – its ring and its dot are dim, whatever its colour.</summary>
        public bool Unlit
        {
            get => unlit;
            set { if (unlit == value) return; unlit = value; MarkDirtyRepaint(); }
        }

        // turns, 0 = the top, clockwise -> the painter's degrees
        static float Deg(float turns) => turns * 360f - 90f;

        void Draw(MeshGenerationContext c)
        {
            Rect r = contentRect;
            if (r.width < 8f || r.height < 8f) return;
            var mid = r.center;
            float rad = Mathf.Min(r.width, r.height) * 0.5f - 3f;
            var p = c.painter2D;
            p.lineWidth = 3f;
            p.lineCap = LineCap.Butt;
            p.strokeColor = SfxColors.Line;
            p.BeginPath(); p.Arc(mid, rad, Deg(-0.375f), Deg(0.375f)); p.Stroke();
            if (bar < 0f) return;
            float to = -0.375f + 0.75f * Mathf.Clamp01(bar), from = centre ? Mathf.Min(0f, to) : -0.375f, end = centre ? Mathf.Max(0f, to) : to;
            if (end - from > 0.002f)
            {
                p.strokeColor = unlit ? SfxColors.Off : accent;
                p.BeginPath(); p.Arc(mid, rad, Deg(from), Deg(end)); p.Stroke();
            }
            float a = Deg(to) * Mathf.Deg2Rad;
            p.fillColor = unlit ? SfxColors.Dim : SfxColors.Text;
            p.BeginPath(); p.Arc(new Vector2(mid.x + rad * Mathf.Cos(a), mid.y + rad * Mathf.Sin(a)), 3.5f, 0f, 360f); p.Fill();
        }
    }

    /// <summary>
    /// A value as the display shows it: the dial, a dim label, the value large and in its unit (hud() and row() in the
    /// firmware). It keeps no value itself: show one with Show / ShowMixed / ShowEmpty, and listen:
    ///   nudged(steps)      dragged up or down, the wheel over the dial, the arrow keys – whole steps, + is up
    ///   typed(text)        a double-click on the value, a text, ENTER
    ///   resetRequested     a double-click on the dial
    ///   chosen(index)      with Choices set: a click on the value opens them as a menu
    ///   dragStarted/Ended  around a drag (one undo step)
    /// View only: Interactive = false – it then looks it too: an outline instead of a tile, a dim ring, a dim value.
    /// </summary>
    public class ValueTile : VisualElement
    {
        public enum Look { Corner, Row }

        readonly Knob knob;
        readonly VisualElement texts;
        readonly TextElement label, value;
        TextField entry;
        bool dragging, moved, interactive = true;
        float startY;
        int sent;

        public event Action<int> nudged;
        public event Action<string> typed;
        public event Action resetRequested;
        public event Action<int> chosen;
        public event Action dragStarted, dragEnded;

        /// <summary>False: it only shows – and looks so (the class sfx-tile--view, a dim dial), takes no focus and no mouse.</summary>
        public bool Interactive
        {
            get => interactive;
            set
            {
                if (interactive == value) return;
                interactive = value;
                focusable = value;
                knob.Unlit = !value;
                EnableInClassList("sfx-tile--view", !value);
                if (!value) { EndTyping(); EndDrag(); }
            }
        }
        /// <summary>How far the mouse moves for one step (SHIFT: four times as far).</summary>
        public float StepPixels = 2f;
        /// <summary>A value that is one of a few names: a click on it opens them.</summary>
        public string[] Choices;
        /// <summary>Which of Choices is the value now (ticked in the menu).</summary>
        public int ChoiceIndex = -1;

        public string LabelText => label.text;
        public string ValueText => value.text;
        public Knob Knob => knob;

        /// <summary>number: the encoder (1-4), 0 = none. right: the dial on the right, the texts aligned right (the display's right corners).</summary>
        public ValueTile(int number = 0, bool right = false, Look look = Look.Corner)
        {
            AddToClassList("sfx-tile");
            if (right) AddToClassList("sfx-tile--right");
            if (look == Look.Row) AddToClassList("sfx-tile--panel");
            focusable = true;
            knob = new Knob { Number = number > 0 ? number.ToString() : "" };
            texts = SfxUI.Box("sfx-tile__texts");
            texts.pickingMode = PickingMode.Ignore;
            label = SfxUI.Text("", "sfx-tile__label");
            value = SfxUI.Text("", "sfx-tile__value");
            texts.Add(label); texts.Add(value);
            Add(knob); Add(texts);

            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            RegisterCallback<KeyDownEvent>(OnKey);
            knob.RegisterCallback<WheelEvent>(OnWheel);
        }

        /// <summary>bar: 0..1, below 0 = no ring value. centre: the ring grows from the top.</summary>
        public void Show(string name, string text, float bar, bool centre, Color accent)
        {
            RemoveFromClassList("sfx-tile--empty");
            label.text = name; value.text = text;
            knob.Set(bar, centre, accent);
        }

        /// <summary>Several things are selected and they differ: "-", and a ring without a value.</summary>
        public void ShowMixed(string name, Color accent) => Show(name, "-", -1f, false, accent);

        /// <summary>Nothing on this knob (it keeps its room).</summary>
        public void ShowEmpty()
        {
            AddToClassList("sfx-tile--empty");
            label.text = ""; value.text = "";
            knob.Set(-1f, false, SfxColors.Line);
        }

        void OnDown(PointerDownEvent e)
        {
            if (!Interactive || e.button != 0 || entry != null) return;
            bool onKnob = knob.worldBound.Contains(e.position);
            if (e.clickCount == 2)
            {
                if (onKnob) resetRequested?.Invoke(); else if (Choices == null) BeginTyping();
                e.StopPropagation();
                return;
            }
            dragging = true; moved = false; sent = 0; startY = e.position.y;
            this.CapturePointer(e.pointerId);
            AddToClassList("sfx-tile--held");
            dragStarted?.Invoke();
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!dragging) return;
            float px = Mathf.Max(0.5f, e.shiftKey ? StepPixels * 4f : StepPixels);
            int steps = (int)((startY - e.position.y) / px);
            if (Mathf.Abs(startY - e.position.y) > 3f) moved = true;
            if (steps != sent) { int d = steps - sent; sent = steps; nudged?.Invoke(d); }
        }

        void OnUp(PointerUpEvent e)
        {
            bool click = dragging && !moved && sent == 0 && !knob.worldBound.Contains(e.position);
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            EndDrag();
            if (click && Choices != null) OpenChoices();
        }

        void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            RemoveFromClassList("sfx-tile--held");
            dragEnded?.Invoke();
        }

        void OnWheel(WheelEvent e)
        {
            if (!Interactive) return;
            int d = e.delta.y < 0f ? 1 : e.delta.y > 0f ? -1 : 0;
            if (d != 0) nudged?.Invoke(d);
            e.StopPropagation();                             // (over the dial the Inspector does not scroll)
        }

        void OnKey(KeyDownEvent e)
        {
            if (!Interactive || entry != null) return;
            int d = e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.RightArrow ? 1 : e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.LeftArrow ? -1 : 0;
            if (d == 0) return;
            nudged?.Invoke(e.shiftKey ? d * 8 : d);
            e.StopPropagation();
        }

        void OpenChoices()
        {
            SfxUI.Menu(this, Choices, ChoiceIndex, pick => chosen?.Invoke(pick));
        }

        /// <summary>The value becomes a field to type in. ENTER takes it (typed). ESC or a click elsewhere is meant to leave it; that was never tried, and a delayed field may take the text when it loses the focus.</summary>
        public void BeginTyping()
        {
            if (entry != null || !Interactive) return;
            entry = new TextField { isDelayed = true };
            entry.AddToClassList("sfx-entry");
            entry.SetValueWithoutNotify(value.text == "-" ? "" : value.text);
            value.style.display = DisplayStyle.None;
            texts.pickingMode = PickingMode.Position;
            texts.Add(entry);
            entry.RegisterValueChangedCallback(e => { typed?.Invoke(e.newValue); schedule.Execute(EndTyping); });
            entry.RegisterCallback<FocusOutEvent>(_ => schedule.Execute(EndTyping));
            entry.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            entry.schedule.Execute(() => { if (entry != null) { entry.Focus(); entry.SelectAll(); } });
        }

        void EndTyping()
        {
            if (entry == null) return;
            var gone = entry;
            entry = null;
            gone.RemoveFromHierarchy();
            texts.pickingMode = PickingMode.Ignore;
            value.style.display = DisplayStyle.Flex;
        }
    }
}
