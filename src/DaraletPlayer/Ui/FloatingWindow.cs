using System;
using System.Numerics;
using ImGuiNET;
using UtilityBelt.Service;
using UtilityBelt.Service.Views;

namespace DaraletPlayer.Ui {
    /// <summary>
    /// A small window outside the main one that stays on screen while you play: the fellowship's vitals, the target, the
    /// tracked target. It sizes itself to what's in it. Unlocked, it has a title bar to drag it by; locked, it has none and
    /// stays put. Right-click it to lock, unlock or hide it. Where it is and whether it's shown are kept in the settings.
    /// </summary>
    internal class FloatingWindow : IDisposable {
        private readonly Hud hud;
        private readonly Settings settings;
        private readonly string id;
        private readonly Vector2 defaultPosition;
        private readonly Action render;

        /// <summary>The saved position is set once, when the window first draws; after that ImGui keeps it.</summary>
        private bool positioned;

        public FloatingWindow(string id, string title, Settings settings, Vector2 defaultPosition, Action render, bool visibleByDefault = true) {
            this.id = id;
            this.settings = settings;
            this.defaultPosition = defaultPosition;
            this.render = render;

            // made here so a window that starts hidden is saved that way
            settings.Window(id, visibleByDefault);

            hud = UBService.Huds.CreateHud(title);
            hud.ShowInBar = false;
            hud.Visible = Placement.Visible;
            hud.OnPreRender += Hud_OnPreRender;
            hud.OnRender += Hud_OnRender;
            hud.OnHide += Hud_OnHide;
        }

        private WindowPlacement Placement => settings.Window(id);

        public bool Visible {
            get => Placement.Visible;
            set {
                if (Placement.Visible == value) {
                    return;
                }

                Placement.Visible = value;
                hud.Visible = value;
                settings.Changed();
            }
        }

        public bool Locked {
            get => Placement.Locked;
            set {
                if (Placement.Locked == value) {
                    return;
                }

                Placement.Locked = value;
                settings.Changed();
            }
        }

        /// <summary>
        /// How big the window is drawn: 1 is normal size. The text is scaled by ImGui; whatever draws the window multiplies
        /// its own widths and heights (bars) by this.
        /// </summary>
        public float Scale {
            get => Placement.ClampedScale;
            set {
                var scale = Math.Max(WindowPlacement.MinScale, Math.Min(WindowPlacement.MaxScale, value));

                if (Placement.Scale == scale) {
                    return;
                }

                Placement.Scale = scale;
                settings.Changed();
            }
        }

        /// <summary>
        /// Puts the window back where it starts out, for one that's been dragged off screen.
        /// </summary>
        public void ResetPosition() {
            Placement.X = null;
            Placement.Y = null;
            positioned = false;
            settings.Changed();
        }

        /// <summary>
        /// The controls the tabs show for this window: shown, locked, reset position and size.
        /// </summary>
        public void RenderControls(string label, string tip) {
            var visible = Visible;

            if (ImGui.Checkbox($"{label}##{id}-visible", ref visible)) {
                Visible = visible;
            }
            Widgets.Tip(tip);

            ImGui.SameLine();
            var locked = Locked;

            if (ImGui.Checkbox($"Lock##{id}-locked", ref locked)) {
                Locked = locked;
            }
            Widgets.Tip("A locked window has no title bar and can't be dragged. Unlock it to move it. You can also right-click the window to lock or unlock it.");

            ImGui.SameLine();

            if (ImGui.SmallButton($"Reset position##{id}-reset")) {
                ResetPosition();
            }
            Widgets.Tip("Moves the window back to where it starts out, in case it's off screen.");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(110);
            var percent = (int)Math.Round(Scale * 100);

            if (ImGui.SliderInt($"Size##{id}-scale", ref percent, (int)(WindowPlacement.MinScale * 100), (int)(WindowPlacement.MaxScale * 100), "%d%%")) {
                Scale = percent / 100f;
            }
            Widgets.Tip("How big the window's text and bars are drawn. Ctrl+click to type a size. You can also right-click the window for Bigger, Smaller and Normal size.");
        }

        private void Hud_OnPreRender(object sender, EventArgs e) {
            try {
                var flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoSavedSettings;

                if (Locked) {
                    flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
                }

                hud.WindowSettings = flags;

                if (!positioned) {
                    ImGui.SetNextWindowPos(new Vector2(Placement.X ?? defaultPosition.X, Placement.Y ?? defaultPosition.Y), ImGuiCond.Always);
                    positioned = true;
                }

                ImGui.SetNextWindowBgAlpha(Locked ? 0.55f : 0.85f);
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        private void Hud_OnRender(object sender, EventArgs e) {
            try {
                // the window may still be drawn once after its plugin has been shut down (the loader swapping builds)
                if (disposed) {
                    return;
                }

                ImGui.SetWindowFontScale(Scale);
                render();
                RenderContextMenu();
                RememberPosition();
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        private void RenderContextMenu() {
            if (!ImGui.BeginPopupContextWindow($"{id}-menu")) {
                return;
            }

            if (ImGui.MenuItem(Locked ? "Unlock" : "Lock")) {
                Locked = !Locked;
            }

            if (ImGui.MenuItem("Bigger")) {
                Scale += 0.1f;
            }

            if (ImGui.MenuItem("Smaller")) {
                Scale -= 0.1f;
            }

            if (ImGui.MenuItem($"Normal size ({Scale * 100:0}% now)")) {
                Scale = 1;
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Hide")) {
                Visible = false;
            }

            ImGui.EndPopup();
        }

        private void RememberPosition() {
            var position = ImGui.GetWindowPos();

            if (Placement.X != position.X || Placement.Y != position.Y) {
                Placement.X = position.X;
                Placement.Y = position.Y;
                settings.Changed();
            }
        }

        /// <summary>
        /// The window's own close button hides it, which is kept like hiding it from a tab.
        /// </summary>
        private void Hud_OnHide(object sender, EventArgs e) {
            try {
                if (Placement.Visible) {
                    Placement.Visible = false;
                    settings.Changed();
                }
            }
            catch (Exception ex) {
                PluginCore.Log(ex);
            }
        }

        private bool disposed;

        public void Dispose() {
            disposed = true;
            hud.OnPreRender -= Hud_OnPreRender;
            hud.OnRender -= Hud_OnRender;
            hud.OnHide -= Hud_OnHide;
            hud.Visible = false;
            hud.Dispose();
        }
    }
}
