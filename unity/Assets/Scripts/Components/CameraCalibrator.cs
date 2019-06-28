using System;
using System.IO;
using UnityEngine;

// The CameraCalibrator is a interactive mode component used to match the parameters
// of the virtual cameras with their real world counterparts. When turned on the 
// calibrator allows to locally translate (camera dolly), rotate (euler) and modify
// the field of view. If a real data set is loaded the camera overlay can be used to
// visually align the two sources (sim and real). Finally the local transformations can
// be colapsed and new position, rotation and fov values for the camera retreived.
//
// Configurable properties:
//   string camera_name - which camera does this calibrator modify,
//   vector3 local_position_delta - local translation to be applied,
//   quaternion local_rotation_delta - local rotation to be applied,
//   float fov_delta - field of view change to be applied.
//
// Read-only properties:
//   string mujoco_position - mujoco xml position vector as string,
//   string mujoco_rotation - mujoco xml rotation quaternion as string,
//   string mujoco_fov - mujoco xml fov as string,
//   string dactyl_camera_setup - full camera config in json.

public class CameraCalibrator : RendererComponent {

    private Camera camera_ = null;
    private Vector3 original_position_ = Vector3.zero;
    private Quaternion original_rotation_ = Quaternion.identity;
    private float original_fov_ = 0.0f;

    private float dolly_x_ = 0.0f;
    private float dolly_y_ = 0.0f;
    private float dolly_z_ = 0.0f;

    private float euler_x_ = 0.0f;
    private float euler_y_ = 0.0f;
    private float euler_z_ = 0.0f;

    private float zoom_ = 0.0f;

    [SerializeField]
    [ConfigProperty]
    public string camera_name_ = "";

    [SerializeField]
    [ConfigProperty]
    public Vector3 local_position_delta_ = Vector3.zero;

    [SerializeField]
    [ConfigProperty]
    public Quaternion local_rotation_delta_ = Quaternion.identity;

    [SerializeField]
    [ConfigProperty]
    public float fov_delta_ = 0.0f;

    // Cache the original position, rotation and fov.
    private void UpdateCamera() {
        Camera[] child_cameras = GetComponentsInChildren<Camera>();
        foreach (Camera child_camera in child_cameras) {
            if (child_camera.name.Equals(camera_name_)) {
                camera_ = child_camera;
                original_fov_ = camera_.fieldOfView;
                original_rotation_ = camera_.transform.rotation;
                original_position_ = camera_.transform.position;
            }
        }
    }

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        camera_name_ = ConfigUtils.GetProperty("camera_name", config, camera_name_);
        UpdateCamera();
        return UpdateComponent(config);
    }

    public override bool UpdateComponent(Orrb.RendererComponentConfig config) {
        string old_camera_name = camera_name_;

        ConfigUtils.GetProperties(this, config);

        if (!camera_name_.Equals(old_camera_name)) {
            UpdateCamera();
        }

        return true;
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        if (camera_ != null) {
            // Apply the local transformations on top of the cached original position,
            // rotation and fov.
            Vector3 dolly = dolly_x_ * camera_.transform.right + dolly_y_ * camera_.transform.up +
                                              dolly_z_ * camera_.transform.forward;
            camera_.transform.position = original_position_ + local_position_delta_ + dolly;
            camera_.transform.rotation = original_rotation_ * local_rotation_delta_ * Quaternion.Euler(
                euler_x_, euler_y_, euler_z_);
            camera_.fieldOfView = original_fov_ + fov_delta_ + zoom_;
        }
        return true;
    }

    public override Orrb.RendererComponentConfig GetConfig() {
        Orrb.RendererComponentConfig config = base.GetConfig();
        ConfigUtils.SetProperty("mujoco_position", config, GetCameraPositionString());
        ConfigUtils.SetProperty("mujoco_rotation", config, GetCameraRotationString());
        ConfigUtils.SetProperty("mujoco_fov", config, GetCameraFovString());
        ConfigUtils.SetProperty("dactyl_camera_setup", config, GetDactylCameraSetupString());
        return config;
    }

    private string GetCameraPositionString() {
        if (camera_ == null) {
            return "";
        }
        return string.Format("{0} {1} {2}", camera_.transform.localPosition.x, camera_.transform.localPosition.y,
                             camera_.transform.localPosition.z);

    }

    private string GetDactylCameraSetupString() {
        if (camera_ == null) {
            return "";
        }

        Vector3 position = camera_.transform.localPosition;
        Quaternion old_rotation = camera_.transform.localRotation;
        camera_.transform.LookAt(camera_.transform.position + camera_.transform.TransformDirection(Vector3.back),
                                         camera_.transform.TransformDirection(Vector3.up));
        Quaternion rotation = camera_.transform.localRotation;
        camera_.transform.localRotation = old_rotation;

        return string.Format("{{'name': '{0}', 'pos': [{1}, {2}, {3}], 'quat': [{4}, {5}, {6}, {7}], 'fovy': {8}}}",
                             camera_.name, position.x, position.y, position.z, rotation.w, rotation.x, rotation.y,
                             rotation.z, camera_.fieldOfView);
    }

    private string GetCameraRotationString() {
        if (camera_ == null) {
            return "";
        }
        Quaternion old_rotation = camera_.transform.localRotation;
        camera_.transform.LookAt(camera_.transform.position + camera_.transform.TransformDirection(Vector3.back),
                                 camera_.transform.TransformDirection(Vector3.up));
        Quaternion local_mujoco_rotation = camera_.transform.localRotation;
        camera_.transform.localRotation = old_rotation;

        return string.Format("{0} {1} {2} {3}", local_mujoco_rotation.w, local_mujoco_rotation.x,
                             local_mujoco_rotation.y, local_mujoco_rotation.z);
    }

    private string GetCameraFovString() {
        if (camera_ == null) {
            return "";
        }
        return string.Format("{0}", camera_.fieldOfView);
    }

    private void Colapse() {
        Vector3 dolly = dolly_x_ * camera_.transform.right + dolly_y_ * camera_.transform.up +
                                              dolly_z_ * camera_.transform.forward;
        local_position_delta_ += dolly;
        local_rotation_delta_ *= Quaternion.Euler(euler_x_, euler_y_, euler_z_);
        fov_delta_ += zoom_;

        dolly_x_ = dolly_y_ = dolly_z_ = euler_x_ = euler_y_ = euler_z_ = zoom_ = 0.0f;
    }

    private void Reset() {
        local_position_delta_ = Vector3.zero;
        local_rotation_delta_ = Quaternion.identity;
        fov_delta_ = 0.0f;

        dolly_x_ = dolly_y_ = dolly_z_ = euler_x_ = euler_y_ = euler_z_ = zoom_ = 0.0f;
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUIField("camera_name", ref camera_name_);

        string label_text = GetCameraPositionString();
        RendererComponent.GUIField("mujoco_position", ref label_text);

        label_text = GetCameraRotationString();
        RendererComponent.GUIField("mujoco_rotation", ref label_text);

        label_text = GetCameraFovString();
        RendererComponent.GUIField("mujoco_fov", ref label_text);

        RendererComponent.GUIHorizontalLine(1);
        RendererComponent.GUIVector3("local_position_delta", ref local_position_delta_);
        RendererComponent.GUIQuaternion("local_rotation_delta", ref local_rotation_delta_);
        RendererComponent.GUIHorizontalLine(1);
        RendererComponent.GUISlider("dolly_x", ref dolly_x_, -0.03f, 0.03f);
        RendererComponent.GUISlider("dolly_y", ref dolly_y_, -0.03f, 0.03f);
        RendererComponent.GUISlider("dolly_z", ref dolly_z_, -0.03f, 0.03f);
        RendererComponent.GUIHorizontalLine(1);
        RendererComponent.GUISlider("euler_x", ref euler_x_, -2.0f, 2.0f);
        RendererComponent.GUISlider("euler_y", ref euler_y_, -2.0f, 2.0f);
        RendererComponent.GUISlider("euler_z", ref euler_z_, -2.0f, 2.0f);
        RendererComponent.GUIHorizontalLine(1);
        RendererComponent.GUISlider("zoom", ref zoom_, -1f, 1f);
        RendererComponent.GUIHorizontalLine(1);

        GUILayout.BeginHorizontal();
        if (RendererComponent.GUIButton("Colapse")) {
            Colapse();
        }
        if (RendererComponent.GUIButton("Reset")) {
            Reset();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
}
