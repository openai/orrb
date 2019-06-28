using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// LightingRig sets up lights in a programatic way and randomizes
// their positions. The rig arranges a number of new lights
// orbiting a specified center point. Mounting distance from the
// center point and the height above it can be separately
// randomized by specifying raspective randomization ranges.
// The newly created lights will be pointed at the center point.
//
// Configurable properties:
//   int light_head_count - how many programatic lights should
//                          be created,
//   float min_light_distance - lower bound for the distance
//                              randomization range,
//   float max_light_distance - upper bound for the distance
//                              randomization range,
//   float min_light_height - lower bound for the hight
//                            randomization range,
//   float max_light_height - upper bound for the hight
//                            randomization range,
//   vector3 target - the absolute location the lights orbit around,
//                    and are targeted at.

public class LightingRig : RendererComponent {

    class LightHead {
        public GameObject rotator = null;
        public Light light = null;

        public void SetUp(Vector3 target, float angle, float height, float distance) {
            rotator.transform.localPosition = target;
            rotator.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.up);
            light.transform.localPosition = Vector3.up * height + Vector3.back * distance;
            light.transform.LookAt(rotator.transform.position, Vector3.up);
        }
    }

    [SerializeField]
    [ConfigProperty]
    public int light_head_count_ = 6;

    [SerializeField]
    [ConfigProperty]
    public float min_light_distance_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public float max_light_distance_ = 1.5f;

    [SerializeField]
    [ConfigProperty]
    public float min_light_height_ = 0.5f;

    [SerializeField]
    [ConfigProperty]
    public float max_light_height_ = 1.5f;

    [SerializeField]
    [ConfigProperty]
    public Vector3 target_ = Vector3.zero;

    private List<LightHead> light_heads_ = new List<LightHead>();

    // Remove the lights created by this randomizer.
    private void ClearLightHeads() {
        foreach (LightHead light_head in light_heads_) {
            light_head.light = null;

            Destroy(light_head.rotator);
            light_head.rotator = null;
        }
        light_heads_.Clear();
    }

    // Set up the lighting rig, create programatic lights.
    private void BuildLightHeads() {
        ClearLightHeads();

        for (int i = 0; i < light_head_count_; ++i) {
            LightHead light_head = new LightHead();

            light_head.rotator = new GameObject(string.Format("light_head_{0}", i));
            light_head.rotator.transform.parent = this.transform;
            light_head.rotator.transform.localPosition = Vector3.zero;

            light_head.light = new GameObject(string.Format("light_{0}", i)).AddComponent<Light>();
            light_head.light.transform.parent = light_head.rotator.transform;
            light_head.light.transform.localPosition = Vector3.zero;
            light_head.light.type = LightType.Spot;
            light_head.light.shadows = LightShadows.Soft;
            light_head.light.range = Mathf.Sqrt(max_light_distance_ * max_light_distance_ +
                                                max_light_height_ + max_light_height_) + 1.0f;
            light_head.light.intensity = 1.0f;
            light_head.light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
            light_head.light.shadowBias = 0.001f;
            light_head.light.shadowNormalBias = 0.0f;
            light_head.light.spotAngle = 30.0f;

            light_heads_.Add(light_head);
        }
    }

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        light_head_count_ = ConfigUtils.GetProperty("light_head_count", config, light_head_count_);
        BuildLightHeads();
        return UpdateComponent(config);
    }

    public override bool UpdateComponent(Orrb.RendererComponentConfig config) {
        int old_light_head_count = light_head_count_;

        ConfigUtils.GetProperties(this, config);

        if (old_light_head_count != light_head_count_) {
            BuildLightHeads();
        }

        return true;
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        foreach (LightHead light_head in light_heads_) {
            // Randomize orbit angle [0.0, 360.0) degrees, and use
            // randomization ranges to determine mounting distance
            // and mounting height.
            light_head.SetUp(target_, Random.Range(0.0f, 360.0f), Random.Range(min_light_height_, max_light_height_),
                             Random.Range(min_light_distance_, max_light_distance_));
        }
        return true;
    }

    public override void DrawEditorGUI() { }
}
