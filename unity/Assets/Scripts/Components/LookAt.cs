using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// LookAt component aligns a given object so that forward arrow points toward the
// specified point. Attach this to a single entity.
//
// Configurable properties:
//   vector3 target_offset - absolute position to look at.

public class LookAt : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public Vector3 target_offset_ = Vector3.zero;

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUIVector3("target_offset", ref target_offset_);
        GUILayout.EndVertical();
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        transform.LookAt(target_offset_, Vector3.up);
        return true;
    }
}
