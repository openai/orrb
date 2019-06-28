using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This component allows manual modification of clipping planes and fov of a scene
// camera. Attach it to a specific camera (by specyfing a hierarchy path in the config)
// as it does not search recursively in children.
//
// Configurable properties:
//   float near_clip - near cliping plane distance in meters,
//   float far_clip - far cliping plane distance in meters,
//   float field_of_view - field of view to set.

public class CameraProperties : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public float near_clip_ = 0.1f;

    [SerializeField]
    [ConfigProperty]
    public float far_clip_ = 20.0f;

    [SerializeField]
    [ConfigProperty]
    public float field_of_view_ = 20.0f;

    private Camera camera_ = null;

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUISlider("near_clip", ref near_clip_, 0.001f, far_clip_);
        RendererComponent.GUISlider("far_clip", ref far_clip_, near_clip_, 100.0f);
        RendererComponent.GUISlider("field_of_view", ref field_of_view_, 1.0f, 180.0f);
        GUILayout.EndVertical();
    }

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        // This component should be attached to an object with an actual camera.
        camera_ = GetComponent<Camera>();
        if (camera_ == null) {
            return false;
        }
        return UpdateComponent(config);
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        if (camera_ == null) {
            return false;
        }
        camera_.nearClipPlane = near_clip_;
        camera_.farClipPlane = far_clip_;
        camera_.fieldOfView = field_of_view_;
        return true;
    }
}
