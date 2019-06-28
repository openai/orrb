using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// MaterialRandomizer assigns random tint, texture and normal map
// to a subset of materials. It also modifies the material's
// glossiness and metallic properties that control how shading
// appears in the Unity PBR model. The textures and normals are
// randomly picked from a small set of random images: checkers,
// patterns, noise, etc... Texture map tiling is also randomized.
//
// Configurable properties:
//   string blacklist_prefix - comma separated list of material
//                             name prefixes to ignore,
//   string whitelist_prefix - comma separated list of material
//                             name prefixes to randomize,
//   float min_metallic - used to clamp the metallic randomization range,
//   float max_metallic - used to clamp the metallic randomization range,
//   float min_glossiness - used to clamp the glossiness randomization range,
//   float max_glossiness - used to clamp the glossiness randomization range,
//   bool randomize_textures - should textures and normal maps be
//                             randomly assigned.

public class MaterialRandomizer : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public string blacklist_prefix_ = "cube";

    [SerializeField]
    [ConfigProperty]
    public string whitelist_prefix_ = "";

    [SerializeField]
    [ConfigProperty]
    public float min_metallic_ = 0.05f;

    [SerializeField]
    [ConfigProperty]
    public float max_metallic_ = 0.25f;

    [SerializeField]
    [ConfigProperty]
    public float min_glossiness_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public float max_glossiness_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public bool randomize_textures_ = false;

    private List<Texture> textures_ = new List<Texture>();

    private List<Texture> normals_ = new List<Texture>();

    private string[] blacklist_prefixes_ = new string[0];

    private string[] whitelist_prefixes_ = new string[0];

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        textures_.AddRange(Resources.LoadAll<Texture>("Textures"));
        normals_.AddRange(Resources.LoadAll<Texture>("Normals"));
        return base.InitializeComponent(config);
    }

    public override bool UpdateComponent(Orrb.RendererComponentConfig config) {
        ConfigUtils.GetProperties(this, config);
        blacklist_prefixes_ = blacklist_prefix_.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        whitelist_prefixes_ = whitelist_prefix_.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        return true;
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer mesh_renderer in renderers) {
            foreach (Material material in mesh_renderer.materials) {
                RandomizeMaterial(material);
            }
        }

        return true;
    }

    private void RandomizeMaterial(Material material) {
        foreach (string prefix in blacklist_prefixes_) {
            if (material.name.StartsWith(prefix, System.StringComparison.Ordinal)) {
                return;
            }
        }

        if (whitelist_prefixes_.Length == 0) {
            DoRandomizeMaterial(material);
        } else {
            foreach (string prefix in whitelist_prefixes_) {
                if (material.name.StartsWith(prefix, System.StringComparison.Ordinal)) {
                    DoRandomizeMaterial(material);
                    return;
                }
            }
        }
    }

    private void DoRandomizeMaterial(Material material) {
        if (material.HasProperty("_Color")) {
            // Do not randomize transparency, keep alpha as it was.
            float alpha = material.color.a;
            material.color = Random.ColorHSV(0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, alpha, alpha);
        }

        if (material.HasProperty("_Metallic")) {
            material.SetFloat("_Metallic", Random.Range(min_metallic_, max_metallic_));
        }

        if (material.HasProperty("_Glossiness")) {
            material.SetFloat("_Glossiness", Random.Range(min_glossiness_, max_glossiness_));
        }

        if (randomize_textures_) {
            if (material.HasProperty("_MainTex")) {
                material.SetTexture("_MainTex", GetRandomTexture());
                material.SetTextureScale("_MainTex", new Vector2(Random.Range(0.4f, 4.0f), Random.Range(0.4f, 4.0f)));
                material.SetTextureOffset("_MainTex", new Vector2(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)));
            }

            if (material.HasProperty("_BumpMap")) {
                material.SetTexture("_BumpMap", GetRandomNormal());
                material.SetTextureScale("_BumpMap", new Vector2(Random.Range(0.4f, 4.0f), Random.Range(0.4f, 4.0f)));
                material.SetTextureOffset("_BumpMap", new Vector2(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)));
            }
        } else {
            if (material.HasProperty("_MainTex")) {
                material.SetTexture("_MainTex", null);
            }

            if (material.HasProperty("_BumpMap")) {
                material.SetTexture("_BumpMap", null);
            }
        }
    }

    private Texture GetRandomTexture() {
        if (textures_.Count > 0) {
            return textures_[Random.Range(0, textures_.Count)];
        } else {
            return null;
        }
    }

    private Texture GetRandomNormal() {
        if (normals_.Count > 0) {
            return normals_[Random.Range(0, normals_.Count)];
        } else {
            return null;
        }
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUIField("blacklist_prefix", ref blacklist_prefix_);
        RendererComponent.GUIField("whitelist_prefix", ref whitelist_prefix_);
        RendererComponent.GUISlider("min_metallic", ref min_metallic_, 0.0f, max_metallic_);
        RendererComponent.GUISlider("max_metallic", ref max_metallic_, min_metallic_, 1.0f);
        RendererComponent.GUISlider("min_glossiness", ref min_glossiness_, 0.0f, max_glossiness_);
        RendererComponent.GUISlider("max_glossiness", ref max_glossiness_, min_glossiness_, 1.0f);
        RendererComponent.GUIToggle("randomize_textures", ref randomize_textures_);
        GUILayout.EndVertical();
    }
}
