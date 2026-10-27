using UnityEngine;
using static MagicGame.ArcaneWidgets;

namespace MagicGame
{
    public partial class GameUI
    {
        const string AttackHelp = "Drag the right stick to aim and fire continuously. Release to stop firing. Skill aiming is independent of the attack stick.";
        const string SkillHelp = "Hold and drag an aimed Active or Ultimate, then release to cast. Drag farther to place a ground skill farther away. Tap without dragging to target the nearest visible enemy in range; with no target, use your last aim or facing direction. Drag onto X and release to cancel without spending cooldown. Shadow dashes where you drag. Area and screen-wide skills remain tap buttons. Daphne can also switch Solar and Lunar stance.";

        // Shared by the prefab migration, HUD builder, and older authored prefab fallback.
        public void EnsureSkillControls()
        {
            var hud = transform.Find("SafeArea/HUD");
            if (hud == null) return;
            var controls = hud.Find("TouchControls");
            if (controls == null) return;
            MakeSkillButtonsCircular(controls);
            foreach (var label in transform.Find("SafeArea/Help").GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (label.name == "HelpSection1") label.text = "<color=#C8A363>02 / AIM & FIRE</color>\n" + AttackHelp;
                if (label.name == "HelpSection2") label.text = "<color=#C8A363>03 / CAST YOUR MAGIC</color>\n" + SkillHelp;
            }
            var overlay = hud.Find("SkillAimOverlay") as RectTransform;
            if (overlay == null) overlay = Rect("SkillAimOverlay", hud, 0, 0, 1, 1);
            if (overlay.GetComponent<CanvasRenderer>() == null) overlay.gameObject.AddComponent<CanvasRenderer>();
            var preview = overlay.GetComponent<SkillAimPreview>();
            if (preview == null) preview = overlay.gameObject.AddComponent<SkillAimPreview>();
            preview.raycastTarget = false;
            overlay.SetSiblingIndex(controls.GetSiblingIndex());
            overlay.gameObject.SetActive(false);
            var cancel = controls.Find("SkillCancel") as RectTransform;
            if (cancel == null)
            {
                cancel = Image("SkillCancel", controls, theme.disc, new Color(.25f, .08f, .12f, .9f), 1, 0, 0, 0).rectTransform;
                cancel.sizeDelta = new Vector2(144, 144);
                Text("SkillCancelLabel", cancel, theme, "X\nCANCEL", 26, 0, 0, 1, 1, true, TMPro.TextAlignmentOptions.Center);
            }
            var cancelLabel = cancel.GetComponentInChildren<TMPro.TMP_Text>(true);
            cancelLabel.enableAutoSizing = true;
            cancelLabel.fontSizeMin = 18;
            cancelLabel.fontSizeMax = 26;
            cancel.gameObject.SetActive(false);
            foreach (SkillSlot slot in System.Enum.GetValues(typeof(SkillSlot)))
            {
                var rect = (RectTransform)controls.Find("HUD" + slot);
                var joystick = rect.GetComponent<SkillJoystick>();
                if (joystick == null) joystick = rect.gameObject.AddComponent<SkillJoystick>();
                var ring = rect.Find(slot + "AimRing") as RectTransform;
                if (ring == null)
                {
                    ring = Image(slot + "AimRing", rect, theme.ring, ArcaneTheme.Cyan, .5f, .5f, 0, 0).rectTransform;
                    ring.sizeDelta = new Vector2(224, 224);
                    var knob = Image(slot + "AimKnob", ring, theme.disc, new Color(.46f, .85f, .91f, .8f), .5f, .5f, 0, 0).rectTransform;
                    knob.sizeDelta = new Vector2(64, 64);
                }
                joystick.slot = slot;
                joystick.ring = ring;
                joystick.knob = (RectTransform)ring.Find(slot + "AimKnob");
                joystick.cancelArea = cancel;
                joystick.preview = preview;
                ring.gameObject.SetActive(false);
            }
            PositionSkillCancel();
        }

        void MakeSkillButtonsCircular(Transform controls)
        {
            foreach (var name in new[] { "Active", "Ultimate", "Stance" })
            {
                var button = controls.Find("HUD" + name);
                var surface = button.GetComponent<UnityEngine.UI.Image>();
                surface.raycastTarget = false;
                surface.enabled = false;
                foreach (var suffix in new[] { "Frame", "Focus", "Fill" })
                {
                    var old = button.Find("HUD" + name + suffix);
                    if (old != null) old.GetComponent<UnityEngine.UI.Image>().enabled = false;
                }
                RoundGraphic(button, "Surface", theme.surfaceTile != null ? theme.surfaceTile : theme.disc, false, true, 0);
                RoundGraphic(button, "Frame", theme.buttonFrame != null ? theme.buttonFrame : theme.ring, theme.buttonFrame != null, false, 1);
                var focus = RoundGraphic(button, "Focus", theme.focusFrame != null ? theme.focusFrame : theme.ring, theme.focusFrame != null, false, 2);
                button.GetComponent<UnityEngine.UI.Button>().targetGraphic = focus;
                var caption = (RectTransform)button.Find(name + "Caption");
                caption.anchorMin = new Vector2(.12f, .10f);
                caption.anchorMax = new Vector2(.88f, .40f);
                var label = caption.GetComponent<TMPro.TMP_Text>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 18;
                label.fontSizeMax = 24;
            }
        }

        static CircularSkillGraphic RoundGraphic(Transform button, string kind, Sprite sprite, bool border, bool raycast, int order)
        {
            string name = button.name + "Circular" + kind;
            var rect = button.Find(name) as RectTransform;
            if (rect == null) rect = Rect(name, button, 0, 0, 1, 1);
            var graphic = rect.GetComponent<CircularSkillGraphic>();
            if (graphic == null) graphic = rect.gameObject.AddComponent<CircularSkillGraphic>();
            graphic.Configure(sprite, border);
            graphic.raycastTarget = raycast;
            rect.SetSiblingIndex(order);
            return graphic;
        }

        void PositionSkillCancel()
        {
            var controls = transform.Find("SafeArea/HUD/TouchControls") as RectTransform;
            if (controls == null) return;
            var cancel = controls.Find("SkillCancel") as RectTransform;
            if (cancel == null) return;
            var ultimate = (RectTransform)controls.Find("HUDUltimate");
            var active = (RectTransform)controls.Find("HUDActive");
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, ultimate);
            var activeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, active);
            float half = cancel.rect.width * .5f;
            Vector2 center = new Vector2((bounds.center.x + activeBounds.center.x) * .5f,
                Mathf.Max(bounds.max.y, activeBounds.max.y) + half + 24);
            center.x = Mathf.Clamp(center.x, controls.rect.xMin + half, controls.rect.xMax - half);
            center.y = Mathf.Clamp(center.y, controls.rect.yMin + half, controls.rect.yMax - half);
            cancel.anchoredPosition = center - new Vector2(controls.rect.xMax, controls.rect.yMin);
        }

        void OnSkillButtonClick(SkillSlot slot)
        {
            var joystick = N("HUD" + slot).GetComponent<SkillJoystick>();
            if (SkillJoystick.Current != null ||
                (joystick != null && (!joystick.isActiveAndEnabled || joystick.SuppressClick)) || GameplayBlocked) return;
            if (_player != null && _player.Skills.GetTargetingMode(slot) != SkillTargetingMode.None)
            {
                if (_player.Skills.CanTarget(slot))
                    GameplayInput.RequestSkillCast(_player.Skills.ResolveSkillCast(slot, Vector2.zero, false));
            }
            else if (slot == SkillSlot.Active) GameplayInput.RequestActive();
            else GameplayInput.RequestUltimate();
        }
    }
}
