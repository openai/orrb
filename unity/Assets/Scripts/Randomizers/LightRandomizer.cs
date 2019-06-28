using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// LightRandomizer randomizes total illumination of the scene and
// the parameters of individual lights. At first a random scene light
// intensity is drawn, then a random weight is drawn for each light and
// the total intensity is weighted between them. Spotlight angle and
// light color hue can be also randomized.
//
// Configurable properties:
//   float min_scene_intensity - minimal sum total intensity of all lights,
//   float max_scene_intensity - maximal sum total intensity of all lights,
//   float min_light_intensity - minimal individual light contribution weight,
//   float max_light_intensity - maximal individual light contribution weight,
//   float min_spotlight_angle - minimal spotlight angle in degrees,
//   float max_spotlight_angle - maximal spotlight angle in degrees,
//   bool randomize_hue - should light color be randomized.

public class LightRandomizer : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public float min_scene_intensity_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public float max_scene_intensity_ = 15.0f;

    [SerializeField]
    [ConfigProperty]
    public float min_light_intensity_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public float max_light_intensity_ = 3.0f;

    [SerializeField]
    [ConfigProperty]
    public float min_spotlight_angle_ = 27.0f;

    [SerializeField]
    [ConfigProperty]
    public float max_spotlight_angle_ = 33.0f;

    [SerializeField]
    [ConfigProperty]
    public bool randomize_hue_ = false;

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        // Find all child light emitters.
        Light[] lights = GetComponentsInChildren<Light>();

        // Draw a total scene light intensity number.
        float scene_intensity = Random.Range(min_scene_intensity_, max_scene_intensity_);
        float sum_intensity_denorm = 0.0f;

        // Go through all lights, and accumulate random weights.
        foreach (Light current_light in lights) {
            if (randomize_hue_) {
                current_light.color = Random.ColorHSV(0.0f, 1.0f, 0.0f, 0.3f, 0.9f, 1.0f);
            } else {
                current_light.color = Color.white;
            }

            // Randomize spot angle.
            current_light.spotAngle = Random.Range(min_spotlight_angle_, max_spotlight_angle_);

            // Draw a random light intensity weight, from the configured
            // range.
            current_light.intensity = Random.Range(min_light_intensity_, max_light_intensity_);
            sum_intensity_denorm += current_light.intensity;
        }

        // Distribute total scene intensity between lights, use:
        //   weight / sum(weights) as factor.
        foreach (Light current_light in lights) {
            current_light.intensity = current_light.intensity * scene_intensity / sum_intensity_denorm;
        }

        return true;
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUISlider("min_scene_intensity", ref min_scene_intensity_, 0.0f, max_scene_intensity_);
        RendererComponent.GUISlider("max_scene_intensity", ref max_scene_intensity_, min_scene_intensity_, 30.0f);
        RendererComponent.GUISlider("min_light_intensity", ref min_light_intensity_, 0.0f, max_light_intensity_);
        RendererComponent.GUISlider("max_light_intensity", ref max_light_intensity_, min_light_intensity_, 30.0f);
        RendererComponent.GUISlider("min_spotlight_angle", ref min_spotlight_angle_, 1.0f, max_spotlight_angle_);
        RendererComponent.GUISlider("max_spotlight_angle", ref max_spotlight_angle_, min_spotlight_angle_, 120.0f);
        RendererComponent.GUIToggle("randomize_hue", ref randomize_hue_);
        GUILayout.EndVertical();
    }
}
