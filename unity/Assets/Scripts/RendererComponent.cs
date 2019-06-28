using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The RendererComponent is a abstract base class for all the components
// that modify, augment and randomize the scene and visual appearance
// of the rendered image.

public abstract class RendererComponent : MonoBehaviour {

    // An OutputContext can be passed when running a component, it
    // is used to record auxiliary outputs, other than the rendered images.
    public interface IOutputContext {
        void OutputInt(string output_name, int value);
        void OutputInts(string output_name, int[] values);
        void OutputFloat(string output_name, float value);
        void OutputFloats(string output_name, float[] values);
        void OutputBool(string output_name, bool value);
        void OutputBools(string output_name, bool[] values);
    };

    // This OutputContext just ignores the auxiliary outputs.
    public class NullOutputContext : RendererComponent.IOutputContext {
        public void OutputBool(string output_name, bool value) { }

        public void OutputBools(string output_name, bool[] values) { }

        public void OutputFloat(string output_name, float value) { }

        public void OutputFloats(string output_name, float[] values) { }

        public void OutputInt(string output_name, int value) { }

        public void OutputInts(string output_name, int[] values) { }
    }

    public virtual bool InitializeComponent(Orrb.RendererComponentConfig config) {
        return UpdateComponent(config);
    }

    // By default just pull the configurable properties from the config.
    public virtual bool UpdateComponent(Orrb.RendererComponentConfig config) {
        ConfigUtils.GetProperties(this, config);
        return true;
    }

    public abstract bool RunComponent(IOutputContext context);

    public abstract void DrawEditorGUI();

    public virtual void DrawSceneGUI() { }

    public virtual Orrb.RendererComponentConfig GetConfig() {
        Orrb.RendererComponentConfig config = new Orrb.RendererComponentConfig();
        ConfigUtils.SetProperties(this, config);
        return config;
    }

    private static string Truncate(string text, int length) {
        if (text.Length > length) {
            return text.Substring(0, length) + "...";
        } else {
            return text;
        }
    }

    // These helper functions are used to generate the gui in the
    // interactive mode.

    public static void GUISlider(string label, ref float slider_value, float min, float max) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Truncate(label, 20), GUILayout.Width(120));
        try {
            slider_value = float.Parse(GUILayout.TextField(string.Format("{0:0.0##########}", slider_value),
                                                           GUILayout.Width(80)));
        } catch {
        }
        slider_value = GUILayout.HorizontalSlider(slider_value, min, max, GUILayout.MaxWidth(170));
        GUILayout.EndHorizontal();
    }

    public static void GUIToggle(string label, ref bool toggle_value) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Truncate(label, 20), GUILayout.Width(120));
        toggle_value = GUILayout.Toggle(toggle_value, "");
        GUILayout.EndHorizontal();

    }

    public static bool GUIField(string label, ref string field_value) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Truncate(label, 20), GUILayout.Width(120));
        string old_value = field_value;
        field_value = GUILayout.TextField(field_value, GUILayout.MaxWidth(250));
        GUILayout.EndHorizontal();
        return !old_value.Equals(field_value);
    }

    public static void GUIHorizontalLine(int height) {
        GUIStyle style = new GUIStyle();
        style.normal.background = Texture2D.whiteTexture;
        style.margin = new RectOffset(4, 4, 6, 6);
        Color c = GUI.color;
        GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        GUILayout.Box(GUIContent.none, style, GUILayout.ExpandWidth(true), GUILayout.Height(height));
        GUI.color = c;
    }

    public static bool GUIButton(string label) {
        return GUILayout.Button(label);
    }

    public static void GUIVector3(string label, ref Vector3 field_value) {
        float x = 0.0f;
        float y = 0.0f;
        float z = 0.0f;
        GUILayout.BeginHorizontal();
        GUILayout.Label(Truncate(label, 20), GUILayout.Width(120));
        try {
            x = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.x), GUILayout.Width(80)));
        } catch { }

        try {
            y = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.y), GUILayout.Width(80)));
        } catch { }

        try {
            z = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.z), GUILayout.Width(80)));
        } catch { }

        field_value = new Vector3(x, y, z);
        GUILayout.EndHorizontal();
    }

    public static void GUIVector3Sliders(string label, ref Vector3 field_value) {
        float x = 0.0f;
        float y = 0.0f;
        float z = 0.0f;
        GUILayout.BeginVertical();
        GUILayout.Label(Truncate(label, 40), GUILayout.Width(400));
        x = GUILayout.HorizontalSlider(field_value.x, -0.5f, 0.5f, GUILayout.Width(400));
        y = GUILayout.HorizontalSlider(field_value.y, -0.5f, 0.5f, GUILayout.Width(400));
        z = GUILayout.HorizontalSlider(field_value.z, -0.5f, 0.5f, GUILayout.Width(400));
        field_value = new Vector3(x, y, z);
        GUILayout.EndVertical();
    }

    public static void GUIQuaternion(string label, ref Quaternion field_value) {
        float x = 0.0f;
        float y = 0.0f;
        float z = 0.0f;
        float w = 1.0f;
        GUILayout.BeginHorizontal();
        GUILayout.Label(Truncate(label, 20), GUILayout.Width(120));
        try {
            x = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.x), GUILayout.Width(60)));
        } catch { }

        try {
            y = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.y), GUILayout.Width(60)));
        } catch { }

        try {
            z = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.z), GUILayout.Width(60)));
        } catch { }

        try {
            w = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", field_value.w), GUILayout.Width(60)));
        } catch { }

        field_value = new Quaternion(x, y, z, w);
        GUILayout.EndHorizontal();
    }

    public static void GUIColor(string label, ref Color field_value) {
        float r = field_value.r;
        float g = field_value.g;
        float b = field_value.b;
        float a = field_value.a;
        GUILayout.BeginHorizontal();
        GUILayout.Label(Truncate(label, 20), GUILayout.Width(120));
        try {
            r = float.Parse(GUILayout.TextField(string.Format("{0:0.0##########}", r),
                                                           GUILayout.Width(80)));
        } catch {
        }
        r = GUILayout.HorizontalSlider(r, 0, 1, GUILayout.MaxWidth(170));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(120));
        try {
            g = float.Parse(GUILayout.TextField(string.Format("{0:0.0##########}", g),
                                                           GUILayout.Width(80)));
        } catch {
        }
        g = GUILayout.HorizontalSlider(g, 0, 1, GUILayout.MaxWidth(170));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(120));
        try {
            b = float.Parse(GUILayout.TextField(string.Format("{0:0.0##########}", b),
                                                           GUILayout.Width(80)));
        } catch {
        }
        b = GUILayout.HorizontalSlider(b, 0, 1, GUILayout.MaxWidth(170));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(120));
        try {
            a = float.Parse(GUILayout.TextField(string.Format("{0:0.0######}", a), GUILayout.Width(60)));
        } catch { }

        field_value = new Color(r, g, b, a);
        GUILayout.EndHorizontal();
    }
}
