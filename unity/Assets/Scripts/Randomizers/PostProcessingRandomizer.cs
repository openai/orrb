using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// This randomizer applies a set of post-processing effects on top of the rendered images.
// There are several post-processing profiles, including color gradient, bloom, ambient
// occlusion and grain noise.
//
// Parameter ranges in Unity:
//   In ColorGrading:
//     hue_shift - [-180, 180]
//     saturation - [-100, 100]
//     contrast - [-100, 100]
//     brightness - [-100, 100]
//     temperature - [-100, 100]
//     tint - [-100, 100]
//   In Bloom:
//     intensity - [0.0, 10.0]
//     diffusion - [1.0, 10.0]
//   In AmbientOcclusion:
//     intensity - [0.0, 4.0]
//   In Grain:
//     intensity - [0.0, 1.0]
//     size - [-0.3, 3.0]
//
// Configurable properties:
//   bool enable_color_grading - enable effects by ColorGrading profile,
//   bool enable_bloom - enable effects by Bloom profile,
//   bool enable_ambient_occlusion - enable effects by AmbientOcclusion profile,
//   bool enable_grain - enable effects by Grain profile,
//   float cg_min_hue_shift - minimum hue shift of all colors,
//   float cg_max_hue_shift - maximum hue shift of all colors,
//   float cg_min_saturation - minimum saturation of all colors,
//   float cg_max_saturation - maximum saturation of all colors,
//   float cg_min_contrast - minimum contrast of all colors,
//   float cg_max_contrast - maximum contrast of all colors,
//   float cg_min_contrast - minimum contrast of all colors,
//   float cg_max_contrast - maximum contrast of all colors,
//   float cg_min_temperature - minimum color temperature to set the white balance to,
//                              a larger number corresponds to a warmer temperature,
//                              while a smaller number induces a cooler one,
//   float cg_max_temperature - maximum color temperature to set the white balance to,
//   float cg_min_tint - minimum range of tint, a large number is more green-ish while a
//                       smaller number is more magenta-ish,
//   float cg_max_tint - maximum range of tint,
//   float bloom_min_intensity - minimum strength of the bloom filter,
//   float bloom_max_intensity - maximum strength of the bloom filter,
//   float bloom_min_diffusion - minimum extent of veiling effects,
//   float bloom_max_diffusion - maximum extent of veiling effects,
//   float ao_min_intensity - minimum degree of darkness added by ambient occlusion,
//   float ao_max_intensity - maximum degree of darkness added by ambient occlusion,
//   float grain_color_probability - probability of enabling colored grain,
//   float grain_min_intensity - minimum grain strength,
//   float grain_max_intensity - maximum grain strength,
//   float grain_min_size - minimum grain particle size,
//   float grain_max_size - maximum grain particle size,

public class PostProcessingRandomizer : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public bool enable_color_grading_ = true;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_hue_shift_ = -10.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_hue_shift_ = 10.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_saturation_ = -20.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_saturation_ = 20.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_contrast_ = -20.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_contrast_ = 20.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_brightness_ = -20.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_brightness_ = 20.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_temperature_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_temperature_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_tint_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_tint_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public bool enable_bloom_ = true;

    [SerializeField]
    [ConfigProperty]
    public float bloom_min_intensity_ = 0.75f;

    [SerializeField]
    [ConfigProperty]
    public float bloom_max_intensity_ = 1.25f;

    [SerializeField]
    [ConfigProperty]
    public float bloom_min_diffusion_ = 2.0f;

    [SerializeField]
    [ConfigProperty]
    public float bloom_max_diffusion_ = 2.5f;

    [SerializeField]
    [ConfigProperty]
    public bool enable_ambient_occlusion_ = true;

    [SerializeField]
    [ConfigProperty]
    public float ao_min_intensity_ = 1.75f;

    [SerializeField]
    [ConfigProperty]
    public float ao_max_intensity_ = 2.25f;

    [SerializeField]
    [ConfigProperty]
    public bool enable_grain_ = false;

    [SerializeField]
    [ConfigProperty]
    public float grain_color_probability_ = 0.75f;

    [SerializeField]
    [ConfigProperty]
    public float grain_min_intensity_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float grain_max_intensity_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float grain_min_size_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float grain_max_size_ = 0.0f;

    private ColorGrading color_grading_ = null;
    private Bloom bloom_ = null;
    private AmbientOcclusion ambient_occlusion_ = null;
    private Grain grain_ = null;

    private PostProcessProfile FindPostprocessingProfile() {
        // We should find one PostProcessVolume per camera.
        PostProcessVolume[] volumes = GetComponentsInChildren<PostProcessVolume>();

        // All the cameras share the same and *only one* profile.
        HashSet<PostProcessProfile> profiles = new HashSet<PostProcessProfile>();
        foreach (PostProcessVolume volume in volumes) {
            profiles.Add(volume.sharedProfile);
        }
        Debug.Assert(profiles.Count == 1);

        // Grab this profile.
        HashSet<PostProcessProfile>.Enumerator em = profiles.GetEnumerator();
        em.MoveNext();
        PostProcessProfile profile = em.Current;

        return profile;
    }

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        // Find the post processing profile first.
        PostProcessProfile profile = FindPostprocessingProfile();

        // Grab the settings we are interested in from this profile.
        profile.TryGetSettings(out color_grading_);
        profile.TryGetSettings(out bloom_);
        profile.TryGetSettings(out ambient_occlusion_);
        profile.TryGetSettings(out grain_);

        Debug.Assert(color_grading_ != null);
        Debug.Assert(bloom_ != null);
        Debug.Assert(ambient_occlusion_ != null);
        Debug.Assert(grain_ != null);

        return base.InitializeComponent(config);
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        if (enable_color_grading_) {
            // Randomize hue, satuation & contrast in ColorGrading object.
            color_grading_.enabled.value = true;
            color_grading_.hueShift.value = Random.Range(cg_min_hue_shift_, cg_max_hue_shift_);
            color_grading_.saturation.value = Random.Range(cg_min_saturation_, cg_max_saturation_);
            color_grading_.contrast.value = Random.Range(cg_min_contrast_, cg_max_contrast_);
            color_grading_.brightness.value = Random.Range(cg_min_brightness_, cg_max_brightness_);
            color_grading_.temperature.value = Random.Range(cg_min_temperature_, cg_max_temperature_);
            color_grading_.tint.value = Random.Range(cg_min_tint_, cg_max_tint_);
        } else {
            color_grading_.enabled.value = false;
        }

        if (enable_bloom_) {
            // Randomize intensity and diffusion in Bloom.
            bloom_.enabled.value = true;
            bloom_.intensity.value = Random.Range(bloom_min_intensity_, bloom_max_intensity_);
            bloom_.diffusion.value = Random.Range(bloom_min_diffusion_, bloom_max_diffusion_);
        } else {
            bloom_.enabled.value = false;
        }

        if (enable_ambient_occlusion_) {
            // Randomize the intensity and radius of shadows in AmbientOcclusion.
            ambient_occlusion_.enabled.value = true;
            ambient_occlusion_.quality.value = AmbientOcclusionQuality.Medium;
            ambient_occlusion_.intensity.value = Random.Range(ao_min_intensity_, ao_max_intensity_);
        } else {
            ambient_occlusion_.enabled.value = false;
        }

        // Add Gains noise if needed.
        if (enable_grain_) {
            // Find Grain
            grain_.enabled.value = true;
            grain_.colored.value = (Random.Range(0.0f, 1.0f) < grain_color_probability_);
            grain_.intensity.value = Random.Range(grain_min_intensity_, grain_max_intensity_);
            grain_.size.value = Random.Range(grain_min_size_, grain_max_size_);
        } else {
            grain_.enabled.value = false;
        }

        return true;
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        // ColorGrading
        RendererComponent.GUIToggle("enable_color_grading", ref enable_color_grading_);
        RendererComponent.GUISlider("cg_min_hue_shift", ref cg_min_hue_shift_, -180.0f, cg_max_hue_shift_);
        RendererComponent.GUISlider("cg_max_hue_shift", ref cg_max_hue_shift_, cg_min_hue_shift_, 180.0f);
        RendererComponent.GUISlider("cg_min_saturation", ref cg_min_saturation_, -100.0f, cg_max_saturation_);
        RendererComponent.GUISlider("cg_max_saturation", ref cg_max_saturation_, cg_min_saturation_, 100.0f);
        RendererComponent.GUISlider("cg_min_contrast", ref cg_min_contrast_, -100.0f, cg_max_contrast_);
        RendererComponent.GUISlider("cg_max_contrast", ref cg_max_contrast_, cg_min_contrast_, 100.0f);
        RendererComponent.GUISlider("cg_min_brightness", ref cg_min_brightness_, -100.0f, cg_max_brightness_);
        RendererComponent.GUISlider("cg_max_brightness", ref cg_max_brightness_, cg_min_brightness_, 100.0f);
        RendererComponent.GUISlider("cg_min_contrast", ref cg_min_contrast_, -100.0f, cg_max_contrast_);
        RendererComponent.GUISlider("cg_max_contrast", ref cg_max_contrast_, cg_min_contrast_, 100.0f);
        RendererComponent.GUISlider("cg_min_temperature", ref cg_min_temperature_, -100.0f, cg_max_temperature_);
        RendererComponent.GUISlider("cg_max_temperature", ref cg_max_temperature_, cg_min_temperature_, 100.0f);
        RendererComponent.GUISlider("cg_min_tint", ref cg_min_tint_, -100.0f, cg_max_tint_);
        RendererComponent.GUISlider("cg_max_tint", ref cg_max_tint_, cg_min_tint_, 100.0f);
        RendererComponent.GUIHorizontalLine(1);

        // Bloom
        RendererComponent.GUIToggle("enable_bloom", ref enable_bloom_);
        RendererComponent.GUISlider("bloom_min_intensity", ref bloom_min_intensity_, 0.0f, bloom_max_intensity_);
        RendererComponent.GUISlider("bloom_max_intensity", ref bloom_max_intensity_, bloom_min_intensity_, 10.0f);
        RendererComponent.GUISlider("bloom_min_diffusion", ref bloom_min_diffusion_, 1.0f, bloom_max_diffusion_);
        RendererComponent.GUISlider("bloom_max_diffusion", ref bloom_max_diffusion_, bloom_min_diffusion_, 10.0f);
        RendererComponent.GUIHorizontalLine(1);

        // Ambient Occlusion
        RendererComponent.GUIToggle("enable_ambient_occlusion", ref enable_ambient_occlusion_);
        RendererComponent.GUISlider("ao_min_intensity", ref ao_min_intensity_, 0.0f, ao_max_intensity_);
        RendererComponent.GUISlider("ao_max_intensity", ref ao_max_intensity_, ao_min_intensity_, 4.0f);
        RendererComponent.GUIHorizontalLine(1);

        // Grain
        RendererComponent.GUIToggle("enable_grain", ref enable_grain_);
        RendererComponent.GUISlider("grain_color_probability", ref grain_color_probability_, 0.0f, 1.0f);
        RendererComponent.GUISlider("grain_min_intensity", ref grain_min_intensity_, 0.0f, grain_max_intensity_);
        RendererComponent.GUISlider("grain_max_intensity", ref grain_max_intensity_, grain_min_intensity_, 1.0f);
        RendererComponent.GUISlider("grain_min_size", ref grain_min_size_, -0.3f, grain_max_size_);
        RendererComponent.GUISlider("grain_max_size", ref grain_max_size_, grain_min_size_, 3.0f);

        GUILayout.EndVertical();
    }
}
