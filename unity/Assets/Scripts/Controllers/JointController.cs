using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A class representing MuJoCo joint elements.

public class JointController : KineticHierarchyController {

    public enum JointType {
        Hinge,
        Slide,
        Ball,
        Free
    };

    // Static counter used when generating names for anonymous joints.
    private static int joint_count_ = 0;

    // Rotation axis for hinges, slide direction for slides.
    [SerializeField]
    public Vector3 axis_ = Vector3.up;

    // Current joint position for hinges and slides.
    [SerializeField]
    public float value_ = 0.0f;

    // Current joint position for free joints.
    [SerializeField]
    public Vector3 value_vector_ = Vector3.zero;

    // Current joint rotation for ball/free joints.
    [SerializeField]
    public Quaternion value_quaternion_ = Quaternion.identity;

    // Range limits for hinges and slides.
    [SerializeField]
    public Vector2 range_ = new Vector2(float.MinValue, float.MaxValue);

    [SerializeField]
    public JointType joint_type_ = JointType.Hinge;

    // This update is provided in order to make debugging (setting joint values)
    // in the Unity Editor easier.
    void Update() {
        if (joint_type_ == JointType.Free) {
            UpdateJoint(value_vector_, value_quaternion_);
        } else if (joint_type_ == JointType.Ball) {
            UpdateJoint(value_quaternion_);
        } else {
            UpdateJoint(value_);
        }
    }

    private static JointType ParseType(string joint_type) {
        if (joint_type == null || "hinge".Equals(joint_type, System.StringComparison.OrdinalIgnoreCase)) {
            return JointType.Hinge;
        } else if ("ball".Equals(joint_type, System.StringComparison.OrdinalIgnoreCase)) {
            return JointType.Ball;
        } else if ("slide".Equals(joint_type, System.StringComparison.OrdinalIgnoreCase)) {
            return JointType.Slide;
        } else if ("free".Equals(joint_type, System.StringComparison.OrdinalIgnoreCase)) {
            return JointType.Free;
        } else {
            Logger.Warning("JointController::ParseType::Unknown joint type: {0}.", joint_type);
            return JointType.Hinge;
        }
    }

    public void Initialize(KineticHierarchyController parent, string joint_name, Vector3 position, Quaternion rotation,
                           Vector3 axis, Vector2 range, string joint_type) {
        if (joint_name == null) {
            joint_name = string.Format("joint_{0}", joint_count_++);
        }
        Initialize(parent, joint_name, position, rotation);
        this.axis_ = axis;
        this.range_ = range;
        this.joint_type_ = ParseType(joint_type);
    }

    // Set a hinge or a slide to a given position, clamped to range limits.
    // Pass radians for hinges. Slides are unitless and depend on configured
    // slide axis.
    public void UpdateJoint(float value) {
        value_ = value;
        if (joint_type_ == JointType.Slide) {
            transform.localPosition = Mathf.Clamp(value_, range_.x, range_.y) * axis_;
        } else if (joint_type_ == JointType.Hinge) {
            transform.localRotation = Quaternion.AngleAxis(Mathf.Clamp(value_, range_.x, range_.y) * Mathf.Rad2Deg, axis_);
        } else {
            Logger.Warning("JointController::UpdateJoint::UpdateJoint(float) called for a ball/free joint: {0}.", name);
        }
    }

    // Set a ball joint to a given rotation.
    public void UpdateJoint(Quaternion value) {
        value_quaternion_ = value;
        if (joint_type_ == JointType.Ball) {
            transform.localRotation = value;
        } else {
            Logger.Warning("JointController::UpdateJoint::UpdateJoint(Quaternion) called for a non ball joint: {0}.",
                           name);
        }
    }

    public void UpdateJoint(Vector3 position, Quaternion rotation) {
        value_vector_ = position;
        value_quaternion_ = rotation;
        if (joint_type_ == JointType.Free) {
            transform.localRotation = rotation;
            transform.localPosition = position;
        } else {
            Logger.Warning("JointController::UpdateJoint::UpdateJoint(Vector3, Quaterion) called for non-free: {0}.",
                           name);
        }
    }
}
