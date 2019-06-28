using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// JointRandomizer component assigns random positions to joints.

public class JointRandomizer : RendererComponent {

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        JointController[] joint_controllers = GetComponentsInChildren<JointController>();
        foreach (JointController joint_controller in joint_controllers) {
            switch (joint_controller.joint_type_) {
            case JointController.JointType.Ball:
                // Assign a random quaternion sampled uniformly from a
                // sphere to a ball joint.
                joint_controller.UpdateJoint(Random.rotationUniform);
                break;
            case JointController.JointType.Hinge:
                // Assign a random value from the joint limit range to
                // a hinge joint.
                float range_min = joint_controller.range_[0];
                float range_max = joint_controller.range_[1];
                if (range_min > float.MinValue && range_max < float.MaxValue) {
                    joint_controller.UpdateJoint(Random.Range(joint_controller.range_[0], joint_controller.range_[1]));
                }
                break;
            case JointController.JointType.Slide:
                // Slide joints don't have ranges, ignore for now.
                break;
            };
        }
        return true;
    }

    public override void DrawEditorGUI() { }
}

