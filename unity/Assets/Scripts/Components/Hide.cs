using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Hide component allows to prevent some objects from being rendered. Three
// types of entities are supported: bodies (with subobjects), geoms and sites.
// For each entity type a list of prefixes can be provided, if an entity name
// matches a prefix the entity will be hidden.
//
// Configurable properties:
//   string body_prefix - a comma separated list of prefixes, bodies to be hidden,
//   string geom_prefix - a comma separated list of prefixes, geoms to be hidden,
//   string site_prefix - a comma separated list of prefixes, sites to be hidden.

public class Hide : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public string body_prefix_ = "";

    [SerializeField]
    [ConfigProperty]
    public string geom_prefix_ = "";

    [SerializeField]
    [ConfigProperty]
    public string site_prefix_ = "";

    private List<BodyController> bodies_ = new List<BodyController>();
    private List<GeomController> geoms_ = new List<GeomController>();
    private List<SiteController> sites_ = new List<SiteController>();

    // Unhide the hidden objects, clear the lists.
    private void Clear() {
        foreach (BodyController body in bodies_) {
            body.gameObject.SetActive(true);
        }
        bodies_.Clear();
        foreach (GeomController geom in geoms_) {
            geom.gameObject.SetActive(true);
        }
        geoms_.Clear();
        foreach (SiteController site in sites_) {
            site.gameObject.SetActive(true);
        }
        sites_.Clear();
    }

    public override bool UpdateComponent(Orrb.RendererComponentConfig config) {
        base.UpdateComponent(config);
        Clear();
        HideBodies();
        HideGeoms();
        HideSites();
        return true;
    }

    // Hide bodies and keep a list of hidden ones.
    private void HideBodies() {
        string[] prefixes = body_prefix_.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (BodyController body in transform.GetComponentsInChildren<BodyController>()) {
            foreach (string prefix in prefixes) {
                if (body.name.StartsWith(prefix, StringComparison.Ordinal)) {
                    bodies_.Add(body);
                    body.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    // Hide geoms and keep a list of hidden ones.
    private void HideGeoms() {
        string[] prefixes = geom_prefix_.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (GeomController geom in transform.GetComponentsInChildren<GeomController>()) {
            foreach (string prefix in prefixes) {
                if (geom.name.StartsWith(prefix, StringComparison.Ordinal)) {
                    geoms_.Add(geom);
                    geom.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    // Hide sites and keep a list of hidden ones.
    private void HideSites() {
        string[] prefixes = site_prefix_.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (SiteController site in transform.GetComponentsInChildren<SiteController>()) {
            foreach (string prefix in prefixes) {
                if (site.name.StartsWith(prefix, StringComparison.Ordinal)) {
                    sites_.Add(site);
                    site.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    // This component does nothing each frame. The actual hiding happens on initialization / update.
    public override bool RunComponent(RendererComponent.IOutputContext context) {
        return true;
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUIField("body_prefix", ref body_prefix_);
        RendererComponent.GUIField("geom_prefix", ref geom_prefix_);
        RendererComponent.GUIField("site_prefix", ref site_prefix_);
        GUILayout.EndVertical();
    }
}
