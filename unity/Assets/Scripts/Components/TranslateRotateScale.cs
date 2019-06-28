using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TRS component performs manual translation, rotation and scaling of the subject.
// Attach it to a specific object.
//
// Configurable properties:
//   vector3 translate - local translation,
//   quaternion rotate - local rotation,
//   vector3 scale - local scale.

public class TranslateRotateScale : RendererComponent {

    [SerializeField][ConfigProperty]
    public Vector3 translate_ = Vector3.zero;

    [SerializeField][ConfigProperty]
    public Quaternion rotate_ = Quaternion.identity;

    [SerializeField][ConfigProperty]
    public Vector3 scale_ = Vector3.one;

    private Vector3 original_translate_ = Vector3.zero;
    private Quaternion original_rotate_ = Quaternion.identity;
    private Vector3 original_scale_ = Vector3.one;

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUIVector3("translate", ref translate_);
        RendererComponent.GUIQuaternion("rotate", ref rotate_);
        RendererComponent.GUIVector3("scale", ref scale_);
        GUILayout.EndVertical();
    }

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        // Cache the original translation, location, scale.
        original_translate_ = transform.localPosition;
        original_rotate_ = transform.localRotation;
        original_scale_ = transform.localScale;
        return UpdateComponent(config);
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        // Apply the local transformation to the cached original values.
        transform.localPosition = original_translate_ + translate_;
        transform.localRotation = original_rotate_ * rotate_;
        Vector3 new_scale = original_scale_;
        new_scale.Scale(scale_);
        transform.localScale = new_scale;
        return true;
    }
}
