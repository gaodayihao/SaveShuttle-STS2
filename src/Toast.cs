using System;
using Godot;

namespace SaveShuttle;

/// <summary>
/// Self-drawn on-screen message. The RitsuLib settings page does not reliably
/// re-render dynamic text after a button action, so every command also reports here.
/// </summary>
internal static class Toast
{
    public static void Show(string message, bool error = false)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                ModLog.Warn("toast: no SceneTree");
                return;
            }

            Node host = tree.CurrentScene ?? tree.Root;
            CanvasLayer layer = new CanvasLayer { Layer = 512, Name = "SaveShuttleToast" };
            host.AddChild(layer);

            Control root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(root);

            Vector2 viewport = host.GetViewport().GetVisibleRect().Size;
            float height = 190f;
            float top = viewport.Y * 0.66f;

            ColorRect background = new ColorRect
            {
                Color = new Color(0f, 0f, 0f, 0.72f),
                Position = new Vector2(viewport.X * 0.06f, top),
                Size = new Vector2(viewport.X * 0.88f, height),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            root.AddChild(background);

            Label label = new Label
            {
                Text = message,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Position = background.Position + new Vector2(16f, 8f),
                Size = background.Size - new Vector2(32f, 16f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 28);
            label.AddThemeColorOverride("font_color", error ? new Color(1f, 0.45f, 0.42f) : new Color(0.92f, 1f, 0.92f));

            Font font = FindFont(host) ?? FindFont(tree.Root);
            if (font != null)
            {
                label.AddThemeFontOverride("font", font);
            }
            root.AddChild(label);

            root.Modulate = new Color(1f, 1f, 1f, 0f);
            Tween tween = root.CreateTween();
            tween.TweenProperty(root, "modulate:a", 1.0f, 0.15f);
            tween.TweenInterval(4.0);
            tween.TweenProperty(root, "modulate:a", 0.0f, 0.9f);
            tween.TweenCallback(Callable.From(() => layer.QueueFree()));

            ModLog.Info("toast: " + message + " (font=" + (font != null) + ")");
        }
        catch (Exception ex)
        {
            ModLog.Warn("toast failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>Borrows a font from any Label already on screen so CJK glyphs render.</summary>
    private static Font FindFont(Node node)
    {
        if (node is Control control)
        {
            Font font = control.GetThemeFont("font");
            if (font != null)
            {
                return font;
            }
        }

        foreach (Node child in node.GetChildren())
        {
            Font found = FindFont(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
