using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This utility aggregates everything on the scene that is needed for rendering.
// It contains the RobotLoader, responsible for loading the MuJoCo XML and building
// the robot representation. It has a StateLoader that is responsible for processing
// the qpos joint position values and setting Unity kinematic hierarchy respectively.
// Finally the ComponentManager keeps track of initialized RendererComponents, that
// perform visual domain randomization and other modifications / augmentations.

public class SceneInstance : MonoBehaviour {

    [SerializeField]
    public StateLoader state_loader_ = null;

    [SerializeField]
    public RobotLoader robot_loader_ = null;

    [SerializeField]
    public ComponentManager component_manager_ = null;

    private int id_ = -1;

    private Dictionary<string, Camera> cameras_ = new Dictionary<string, Camera>();

    public void SetId(int id) {
        id_ = id;
    }

    public int GetId() {
        return id_;
    }

    public bool Initialize(string robot_xml_path, string mapping_path, string asset_basedir) {
        gameObject.name = string.Format("Scene({0})", robot_xml_path);

        if (!robot_loader_.LoadRobot(robot_xml_path, asset_basedir)) {
            Logger.Error("SceneInstance::InitializeLocal::Could not load robot from: {0}.", robot_xml_path);
            return false;
        }

        if (!state_loader_.Initialize(mapping_path)) {
            Logger.Error("SceneInstance::InitializeLocal::Could not initialize state loader mappings from: {0}.", mapping_path);
            return false;
        }

        foreach (Camera scene_camera in GetComponentsInChildren<Camera>()) {
            cameras_.Add(scene_camera.name, scene_camera);
        }

        return true;
    }

    public bool AddComponent(string type, string name, string path, Orrb.RendererComponentConfig config, bool enabled) {
        return component_manager_.AddComponent(type, name, path, config, enabled);
    }

    public bool RemoveComponent(string name) {
        return component_manager_.RemoveComponent(name);
    }

    public bool UpdateComponent(string name, Orrb.RendererComponentConfig config) {
        return component_manager_.UpdateComponent(name, config);
    }

    public bool NextState() {
        return state_loader_.NextState();
    }

    public bool UpdateState(IList<float> state) {
        return state_loader_.UpdateState(state);
    }

    public List<Camera> GetCameras(IList<string> camera_names) {

        List<Camera> scene_cameras = new List<Camera>();
        foreach (string camera_name in camera_names) {
            if (!cameras_.ContainsKey(camera_name)) {
                Logger.Error("SceneManager::GetCamera::Cannot find camera: {0}.", camera_name);
                return null;
            }
            scene_cameras.Add(cameras_[camera_name]);
        }
        return scene_cameras;
    }

    public List<Camera> GetCameras() {
        List<Camera> cameras = new List<Camera>();
        foreach (Camera scene_camera in cameras_.Values) {
            cameras.Add(scene_camera);
        }
        return cameras;
    }

    public GameObject GetRobot() {
        return robot_loader_.GetRobot();
    }

    public StateLoader GetStateLoader() {
        return state_loader_;
    }

    public ComponentManager GetComponentManager() {
        return component_manager_;
    }
}
