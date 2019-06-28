using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This randomizer can be used to modify material appearance close
// to a calibrated value. The main idea is to use the HSV representation
// and separately randomize: hue, saturation and value. All three
// dimensions use separate randomization ranges and radii. Additionally,
// the hue spectrum wraps around the red-violet edges. Apart from the main
// material color, emission can also be randomized: with certain probability
// emissive materials will emit light, intensity of emission controlled by
// a separate randomization range and radius. Two parameters from the Unity
// PBR model are also randomized: glossiness and metallic.
//
// Parameter ranges in Unity:
//   hue - [0.0, 1.0) wrapped,
//   saturation - [0.0, 1.0],
//   value - [0.0, 1.0],
//   emission - [0.0, +inf), how much light does it produce, values around
//              [2.0, 5.0] make lot sense,
//   metallic - [0.0, 1.0] is it a diffusive or specular material,
//   glossiness - [0.0, 1.0] is it smooth or rough.
//
// Configurable properties:
//   string material_prefix - comma separated list of name prefixes for
//                            materials to randomize,
//   string material_blacklist_prefix - comma separated list of name
//                                      prefixes for materials to exclude,
//   float hue_radius - maximal perturbation of hue,
//                      (hue normalized to [0.0, 1.0), wraps around),
//   float saturation_radius - maximal perturbation of saturation,
//                      (saturation normalized to [0.0, 1.0]),
//   float value_radius - maximal perturbation of value,
//                        (value normalized to [0.0, 1.0]),
//   float emission_radius - maximal perturbation of emissive strength,
//   float min_saturation - used to clamp saturation after randomization,
//   float max_saturation - used to clamp saturation after randomization,
//   float min_value - used to clamp value after randomization,
//   float max_value - used to clamp value after randomization,
//   float min_glossiness - used to clamp glossines after randomization,
//   float max_glossiness - used to clamp glossines after randomization,
//   float min_metallic - used to clamp metallic after randomization,
//   float max_metallic - used to clamp metallic after randomization,
//   float min_emission - used to clamp emission after randomization,
//   float max_emission - used to clamp emission after randomization,
//   float emission_probability - what is the probability an emissive material
//                                will actually emit light, use it to model
//                                blünkenlights in das komputerrmaschine.

public class MaterialFixedHueRandomizer : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public string material_prefix_ = "cube";

    [SerializeField]
    [ConfigProperty]
    public string material_blacklist_prefix_ = "";

    [SerializeField]
    [ConfigProperty]
    public float hue_radius_ = 0.02f;

    [SerializeField]
    [ConfigProperty]
    public float saturation_radius_ = 0.15f;

    [SerializeField]
    [ConfigProperty]
    public float value_radius_ = 0.15f;

    [SerializeField]
    [ConfigProperty]
    public float emission_radius_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float min_saturation_ = 0.5f;

    [SerializeField]
    [ConfigProperty]
    public float max_saturation_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public float min_value_ = 0.5f;

    [SerializeField]
    [ConfigProperty]
    public float max_value_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public float min_metallic_ = 0.05f;

    [SerializeField]
    [ConfigProperty]
    public float max_metallic_ = 0.15f;

    [SerializeField]
    [ConfigProperty]
    public float min_glossiness_ = 0.05f;

    [SerializeField]
    [ConfigProperty]
    public float max_glossiness_ = 0.15f;

    [SerializeField]
    [ConfigProperty]
    public float min_emission_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float max_emission_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float emission_probability_ = 0.25f;

    private struct HSVMaterial {
        public float h;
        public float s;
        public float v;
        public float e;
        public Material m;
    };

    private List<HSVMaterial> initial_colors_ = new List<HSVMaterial>();

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        material_prefix_ = ConfigUtils.GetProperty("material_prefix", config, material_prefix_);
        material_blacklist_prefix_ = ConfigUtils.GetProperty("material_blacklist_prefix", config,
                                                             material_blacklist_prefix_);
        UpdateMaterialList();
        return UpdateComponent(config);
    }

    // Cache original parameters in the materials.
    private void UpdateMaterialList() {
        initial_colors_.Clear();
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        string[] prefixes = material_prefix_.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        string[] blacklist = material_blacklist_prefix_.Split(new char[] { ',' },
                                                              System.StringSplitOptions.RemoveEmptyEntries);

        foreach (MeshRenderer mesh_renderer in renderers) {
            foreach (Material material in mesh_renderer.materials) {
                // Ignore if name on blacklisted prefix list.
                bool blacklisted = false;
                foreach (string blacklist_prefix in blacklist) {
                    if (material.name.StartsWith(blacklist_prefix, System.StringComparison.Ordinal)) {
                        blacklisted = true;
                        break;
                    }
                }
                if (!blacklisted) {
                    // Process if name matches the allowed prefix list.
                    foreach (string prefix in prefixes) {
                        if (material.name.StartsWith(prefix, System.StringComparison.Ordinal)) {
                            HSVMaterial hsvm = new HSVMaterial();
                            Color.RGBToHSV(material.color, out hsvm.h, out hsvm.s, out hsvm.v);
                            // If this is an emissive material with emission set up,
                            // cache the original emissive value.
                            if (material.IsKeywordEnabled("_EMISSION") && material.HasProperty("_EmissionColor")) {
                                hsvm.e = material.GetColor("_EmissionColor").a;
                            } else {
                                hsvm.e = 0.0f;
                            }
                            hsvm.m = material;
                            initial_colors_.Add(hsvm);
                        }
                    }
                }
            }
        }
    }

    public override bool UpdateComponent(Orrb.RendererComponentConfig config) {
        string old_material_prefix = material_prefix_;
        string old_material_blacklist_prefix = material_blacklist_prefix_;

        ConfigUtils.GetProperties(this, config);

        if (!material_prefix_.Equals(old_material_prefix) ||
            !material_blacklist_prefix_.Equals(old_material_blacklist_prefix)) {
            UpdateMaterialList();
        }

        return true;
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        foreach (HSVMaterial hsvm in initial_colors_) {
            // Calculate the clamped randomization ranges.
            float hue = Mathf.Repeat(Random.Range(hsvm.h - hue_radius_, hsvm.h + hue_radius_), 1.0f);
            float min_s = Mathf.Max(min_saturation_, hsvm.s - saturation_radius_);
            float max_s = Mathf.Min(max_saturation_, hsvm.s + saturation_radius_);
            float min_v = Mathf.Max(min_value_, hsvm.v - value_radius_);
            float max_v = Mathf.Min(max_value_, hsvm.v + value_radius_);
            float min_e = Mathf.Max(min_emission_, hsvm.e - emission_radius_);
            float max_e = Mathf.Min(max_emission_, hsvm.e + emission_radius_);

            // We will keep the alpha exactly as original, do not randomize
            // transparency.
            float alpha = hsvm.m.color.a;

            // Randomize basic properties.
            if (hsvm.m.HasProperty("_Color")) {
                hsvm.m.color = Random.ColorHSV(hue, hue, min_s, max_s, min_v, max_v, alpha, alpha);
            }

            if (hsvm.m.HasProperty("_Metallic")) {
                hsvm.m.SetFloat("_Metallic", Random.Range(min_metallic_, max_metallic_));
            }

            if (hsvm.m.HasProperty("_Glossiness")) {
                hsvm.m.SetFloat("_Glossiness", Random.Range(min_glossiness_, max_glossiness_));
            }

            // If the material was originally emissive, randomize emission
            // with some probability (blinkenlights).
            if (hsvm.m.IsKeywordEnabled("_EMISSION") && hsvm.m.HasProperty("_EmissionColor")) {
                if (emission_probability_ > Random.Range(0.0f, 1.0f)) {
                    hsvm.m.SetColor(
                        "_EmissionColor",
                        Random.ColorHSV(hue, hue, min_s, max_s, min_v, max_v, alpha, alpha) * Random.Range(min_e, max_e));
                } else {
                    hsvm.m.SetColor("_EmissionColor", Color.black);
                }
            }
        }
        return true;
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUIField("material_prefix", ref material_prefix_);
        RendererComponent.GUIField("material_blacklist_prefix", ref material_blacklist_prefix_);
        RendererComponent.GUIHorizontalLine(1);

        RendererComponent.GUISlider("hue_radius", ref hue_radius_, 0.0f, 0.5f);
        RendererComponent.GUIHorizontalLine(1);

        RendererComponent.GUISlider("saturation_radius", ref saturation_radius_, 0.0f, 1.0f);
        RendererComponent.GUISlider("min_saturation", ref min_saturation_, 0.0f, max_saturation_);
        RendererComponent.GUISlider("max_saturation", ref max_saturation_, min_saturation_, 1.0f);
        RendererComponent.GUIHorizontalLine(1);

        RendererComponent.GUISlider("value_radius", ref value_radius_, 0.0f, 1.0f);
        RendererComponent.GUISlider("min_value", ref min_value_, 0.0f, max_value_);
        RendererComponent.GUISlider("max_value", ref max_value_, min_value_, 1.0f);
        RendererComponent.GUIHorizontalLine(1);

        RendererComponent.GUISlider("emission_probability", ref emission_probability_, 0.0f, 1.0f);
        RendererComponent.GUISlider("emission_radius", ref emission_radius_, 0.0f, 10.0f);
        RendererComponent.GUISlider("min_emission", ref min_emission_, 0.0f, max_emission_);
        RendererComponent.GUISlider("max_emission", ref max_emission_, min_emission_, 10.0f);
        RendererComponent.GUIHorizontalLine(1);

        RendererComponent.GUISlider("min_metallic", ref min_metallic_, 0.0f, max_metallic_);
        RendererComponent.GUISlider("max_metallic", ref max_metallic_, min_metallic_, 1.0f);
        RendererComponent.GUIHorizontalLine(1);

        RendererComponent.GUISlider("min_glossiness", ref min_glossiness_, 0.0f, max_glossiness_);
        RendererComponent.GUISlider("max_glossiness", ref max_glossiness_, min_glossiness_, 1.0f);
        GUILayout.EndVertical();
    }
}
