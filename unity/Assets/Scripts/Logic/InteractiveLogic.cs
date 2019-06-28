using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Google.Protobuf;
using UnityEngine;

// This is the main part of the standalone renderer. This script loads
// the scene, sets up the server or the interactive mode.

public class InteractiveLogic : MonoBehaviour {

    enum State {
        Init,
        MainLoop,
        Failed,
        Finished
    };

    public enum Mode {
        Interactive,
        Server
    };

    [SerializeField]
    public SceneManager scene_manager_ = null;

    [SerializeField]
    public RenderServer render_server_ = null;

    [SerializeField]
    public Recorder recorder_ = null;

    [SerializeField]
    [Flag]
    public Mode mode_ = Mode.Interactive;

    // The renderer components config file location.
    [SerializeField]
    [Flag]
    public string renderer_config_path_ = null;

    // The MuJoCo XML scene file location.
    [SerializeField]
    [Flag]
    public string model_xml_path_ = null;

    // The joint name to qpos index mapping file location.
    [SerializeField]
    [Flag]
    public string model_mapping_path_ = null;

    // The state data (qposes and images) location for interactive mode.
    [SerializeField]
    [Flag]
    public string model_state_path_ = null;

    // Assets basedir, used to resolve relative resource paths.
    [SerializeField]
    [Flag]
    public string asset_basedir_ = ".";

    // Process id of the parent process. Used to shutdown when parent is dead.
    [SerializeField]
    [Flag]
    public int parent_pid_ = -1;

    private State current_state_ = State.Init;
    private SceneInstance local_scene_instance_ = null;
    private List<Camera> scene_cameras_ = new List<Camera>();
    private int current_camera_ = 0;
    private bool show_ui_ = true;
    private bool anchor_left_ = true;
    private string config_save_path_ = "config.json";

    void Start() {
        Flags.InitFlags(this, "main");
        renderer_config_path_ = ConfigUtils.ResolveFile(asset_basedir_, renderer_config_path_);
        model_mapping_path_ = ConfigUtils.ResolveFile(asset_basedir_, model_mapping_path_);
        model_state_path_ = ConfigUtils.ResolveFile(asset_basedir_, model_state_path_);
        config_save_path_ = string.Format("{0}.new", renderer_config_path_);
    }

    void Update() {
        switch (current_state_) {
        case State.Init:
            current_state_ = State.MainLoop;
            local_scene_instance_ = scene_manager_.CreateSceneInstance();

            Orrb.RendererConfig renderer_config = LoadConfig(renderer_config_path_);

            // If the renderer config contains model and mapping paths, use them.

            if (renderer_config.ModelXmlPath.Length > 0) {
                model_xml_path_ = renderer_config.ModelXmlPath;
            }

            if (renderer_config.ModelMappingPath.Length > 0) {
                model_mapping_path_ = renderer_config.ModelMappingPath;
            }

            local_scene_instance_.Initialize(model_xml_path_, model_mapping_path_, asset_basedir_);

            foreach (Orrb.RendererComponent renderer_component in renderer_config.Components) {
                local_scene_instance_.GetComponentManager().AddComponent(
                    renderer_component.Type,
                    renderer_component.Name,
                    renderer_component.Path,
                    renderer_component.Config,
                    mode_ == Mode.Server  // Enable by default in server mode. 
                );
            }

            if (mode_ == Mode.Server) {
                // In server mode: resize the useless window, start the GRPC
                // server and turn on capture.
                Screen.SetResolution(50, 50, false);
                render_server_.Initialize(recorder_, local_scene_instance_);
                recorder_.Initialize(render_server_);
            } else {
                // In interactive mode: load state from files, set up active
                // cameras.
                local_scene_instance_.GetStateLoader().InitializeStateStream(model_state_path_);
                scene_cameras_ = local_scene_instance_.GetCameras();
                ToggleCamera(current_camera_);
            }

            if (parent_pid_ != -1) {
                // If parent pid was provided start a parent watchdog coroutine.
                StartCoroutine(ParentProcessWatch());
            }

            break;
        case State.MainLoop:
        default:
            if (mode_ == Mode.Server) {
                render_server_.ProcessRequests();
            } else {
                local_scene_instance_.GetComponentManager().RunComponents(new RendererComponent.NullOutputContext());
            }
            break;
        }
    }

    // This coroutine keeps track of the parent process. If the parent is
    // dead it will stop the standalone renderer.
    private IEnumerator ParentProcessWatch() {
        Process parent = null;
        try {
            parent = Process.GetProcessById(parent_pid_);
        } catch (Exception e) {
            Logger.Error("Exception geting parent process: {0}, {1}.", parent_pid_, e.Message);
            Application.Quit();
        }

        if (parent == null) {
            Logger.Error("Parent process is null: {0}.", parent_pid_);
            Application.Quit();
        }

        Logger.Info("Starting parent process watchdog: {0}.", parent_pid_);

        while (true) {
            yield return new WaitForSecondsRealtime(3);

            if (parent.HasExited) {
                Logger.Info("Parent has exited, quitting.");
                Application.Quit();
                yield break;
            }
        }
    }

    private Orrb.RendererConfig LoadConfig(string path) {
        return Orrb.RendererConfig.Parser.ParseJson(File.ReadAllText(path));
    }

    private static Orrb.RendererComponentConfig ParseConfig(string config) {
        return Orrb.RendererComponentConfig.Parser.ParseJson(config.Replace('\'', '"'));
    }

    // Loop through cameras, disable all but one selected.
    private void ToggleCamera(int new_camera) {
        foreach (Camera scene_camera in scene_cameras_) {
            scene_camera.enabled = false;
        }
        current_camera_ = new_camera;
        scene_cameras_[current_camera_].enabled = true;
    }

    // Make the previous camera active, in interactive mode.
    private void PreviousCamera() {
        if (scene_cameras_.Count == 0) {
            return;
        }
        current_camera_ = (current_camera_ + scene_cameras_.Count - 1) % scene_cameras_.Count;
        ToggleCamera(current_camera_);
    }

    // Make the next camera active, in interactive mode.
    private void NextCamera() {
        if (scene_cameras_.Count == 0) {
            return;
        }
        current_camera_ = (current_camera_ + 1) % scene_cameras_.Count;
        ToggleCamera(current_camera_);
    }

    void OnGUI() {
        if (current_state_ != State.MainLoop || mode_ == Mode.Server) {
            return;
        }

        GUI.skin.toggle.fontSize = 12;
        GUI.skin.label.fontSize = 12;

        local_scene_instance_.GetComponentManager().DrawSceneGUI();
        local_scene_instance_.GetStateLoader().DrawSceneGUI();

        GUILayout.BeginArea(new Rect(anchor_left_ ? 10 : Screen.width - 410, 10, 400, Screen.height - 20));
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();

        if (!anchor_left_) {
            GUILayout.FlexibleSpace();
        }
        if (GUILayout.Button(show_ui_ ? "-" : "+", GUILayout.Width(20))) {
            show_ui_ = !show_ui_;
        }

        if (GUILayout.Button(anchor_left_ ? ">" : "<", GUILayout.Width(20))) {
            anchor_left_ = !anchor_left_;
        }

        if (anchor_left_) {
            GUILayout.FlexibleSpace();
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(3);

        if (show_ui_) {
            GUILayout.BeginHorizontal();
            config_save_path_ = GUILayout.TextField(config_save_path_, GUILayout.Width(310));
            if (GUILayout.Button("Save config", GUILayout.Width(80))) {
                Orrb.RendererConfig config = local_scene_instance_.GetComponentManager().GetConfig();
                JsonFormatter formatter = new JsonFormatter(JsonFormatter.Settings.Default);
                File.WriteAllText(config_save_path_, formatter.Format(config));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" < ", GUILayout.Width(20))) {
                PreviousCamera();
            }
            if (GUILayout.Button(" > ", GUILayout.Width(20))) {
                NextCamera();
            }
            GUILayout.Space(10);
            GUILayout.Label(scene_cameras_.Count > current_camera_ ? scene_cameras_[current_camera_].name : "-",
                            GUILayout.Width(300));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            local_scene_instance_.GetStateLoader().DrawEditorGUI();

            GUILayout.Space(3);
            local_scene_instance_.GetComponentManager().DrawEditorGUI();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

}
