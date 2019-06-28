using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The CameraRandomizer modifies the position, rotation and field of view
// of the scene cameras. There are three modes of operation: 'Jitter', 'Orbit'
// and 'Both'. 'Jitter' adds random local perturabtions. 'Orbit' randomly orbits
// the camera around a specific absolute location. Finally 'Both' applies both.
//
// Configurable properties:
//   enum mode - 'Jitter', 'Orbit' or 'Both',
//   float position_radius - maximal position perturbation distance, in meters,
//                           used in 'Jitter' mode,
//   float quat_radius - maximal rotation perturbation magnitude, in radians,
//                       used in 'Jitter' mode,
//   float fov_radius - maximal field of view perturbation, in degrees, used
//                      in 'Jitter' mode,
//   vector3 orbit_center - the location around the camera should be randomly
//                          rotated, used in 'Orbit' mode.

public class CameraRandomizer : RendererComponent {

    public enum Mode {
        Jitter,
        Orbit,
        Both
    }

    [SerializeField]
    [ConfigProperty]
    public Mode mode_ = Mode.Jitter;

    [SerializeField]
    [ConfigProperty]
    public float position_radius_ = 0.02f;

    [SerializeField]
    [ConfigProperty]
    public float fov_radius_ = 1.0f;

    [SerializeField]
    [ConfigProperty]
    public float quat_radius_ = 0.03f;

    [SerializeField]
    [ConfigProperty]
    public Vector3 orbit_center_ = Vector3.zero;

    private struct CameraState {
        public float fov;
        public Vector3 pos;
        public Quaternion rot;
        public Camera camera;
    };

    private List<CameraState> initial_camera_states_ = new List<CameraState>();

    // Cache the original camera positions, rotations and field of view values.
    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        Camera[] cameras = GetComponentsInChildren<Camera>();
        foreach (Camera current_camera in cameras) {
            CameraState camera_state = new CameraState();
            camera_state.camera = current_camera;
            camera_state.fov = current_camera.fieldOfView;
            camera_state.pos = current_camera.transform.localPosition;
            camera_state.rot = current_camera.transform.localRotation;
            initial_camera_states_.Add(camera_state);
        }

        return UpdateComponent(config);
    }

    public override bool RunComponent(RendererComponent.IOutputContext context) {
        // Calculate the perturbation ranges for position, location and field of view.
        float fov_min = -fov_radius_ / 2.0f;
        float fov_max = fov_radius_ / 2.0f;
        float pos_min = -position_radius_ / 2.0f;
        float pos_max = position_radius_ / 2.0f;
        float rot_min = -quat_radius_ / 2.0f;
        float rot_max = quat_radius_ / 2.0f;

        if (mode_ == Mode.Jitter || mode_ == Mode.Both) {
            // In 'Jitter' mode, apply random local perturbations on top of the
            // original cached state.
            foreach (CameraState camera_state in initial_camera_states_) {
                camera_state.camera.fieldOfView = camera_state.fov + Random.Range(fov_min, fov_max);
                camera_state.camera.transform.localPosition = camera_state.pos + new Vector3(Random.Range(pos_min, pos_max), Random.Range(pos_min, pos_max), Random.Range(pos_min, pos_max));
                Vector3 axis = Random.rotationUniform * Vector3.up;
                camera_state.camera.transform.localRotation = camera_state.rot * Quaternion.AngleAxis(Random.Range(rot_min, rot_max) * Mathf.Rad2Deg, axis);
            }
        }

        if (mode_ == Mode.Orbit || mode_ == Mode.Both) {
            // In 'Orbit' mode rotate the camera around a line, going straight
            // up from the 'orbit_center' point.
            foreach (CameraState camera_state in initial_camera_states_) {
                camera_state.camera.transform.RotateAround(orbit_center_, Vector3.up, Random.Range(0.0f, 360.0f));
            }
        }

        return true;
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        RendererComponent.GUISlider("position_radius", ref position_radius_, 0.0f, 1.0f);
        RendererComponent.GUISlider("fov_radius", ref fov_radius_, 0.0f, 20.0f);
        RendererComponent.GUISlider("quat_radius", ref quat_radius_, 0.0f, 1.0f);
        RendererComponent.GUIVector3("orbit_center", ref orbit_center_);
        GUILayout.EndVertical();
    }
}
