using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

// The StateLoader is responsible for updating the joint positions. The state loader can
// use a csv file with example states loaded from disk - this is used in interactive / debug
// mode. The state loader can also accept a list of qposes / joint positions - used by the
// RenderServer in server mode.

public class StateLoader : MonoBehaviour {

    // A mapping between the joint name and the qpos index in the state data.
    public class JointDefinition {
        public string name;
        public int index;

        public JointDefinition(string name, int index) {
            this.name = name;
            this.index = index;
        }
    }

    private JointController[] mapping_ = new JointController[0];

    private List<float[]> states_ = new List<float[]>();

    private Dictionary<string, Texture2D[]> footage_ = new Dictionary<string, Texture2D[]>();

    private int current_frame_ = 0;
    private Texture2D reference_image_texture_ = null;
    private float reference_overlay_alpha_ = 0.5f;
    private bool playing_ = false;

    private Material overlay_material_ = null;

    [SerializeField]
    public bool automatic_update_ = false;

    // Update is called once per frame
    void Update() {
        if (automatic_update_) {
            NextState();
        }
    }

    public void Toggle() {
        automatic_update_ = !automatic_update_;
    }

    // Load the joint:index mapping from the file.
    public bool Initialize(string mapping_path) {
        StreamReader reader = new StreamReader(mapping_path);
        List<JointDefinition> joint_definitions = new List<JointDefinition>();

        string line = "";

        while ((line = reader.ReadLine()) != null) {
            string[] split = line.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            joint_definitions.Add(new JointDefinition(split[0], int.Parse(split[1])));
        }

        return Initialize(joint_definitions);
    }

    public bool Initialize(IList<JointDefinition> joint_definitions) {
        int max = -1;
        foreach (JointDefinition joint_definition in joint_definitions) {
            max = Mathf.Max(max, joint_definition.index);
        }
        max++;

        mapping_ = new JointController[max];
        for (int i = 0; i < max; ++i) {
            mapping_[i] = null;
        }

        foreach (JointDefinition joint_definition in joint_definitions) {
            mapping_[joint_definition.index] = FindJoint(joint_definition.name);
        }

        overlay_material_ = new Material(Shader.Find("Unlit/Overlay"));
        return true;
    }

    // Open the csv file with the example states and load them. If there is footage for the states,
    // load it into textures.
    public bool InitializeStateStream(string stream_dir) {
        string state_path = string.Format("{0}/qpos.csv", stream_dir);

        StreamReader state_reader = new StreamReader(state_path);
        string line = "";
        while ((line = state_reader.ReadLine()) != null) {
            string[] split = line.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            float[] state = new float[split.Length];
            for (int i = 0; i < split.Length; ++i) {
                state[i] = float.Parse(split[i]);
            }
            states_.Add(state);
        }
        current_frame_ = 0;
        UpdateState(states_[0]);

        string footage_path = string.Format("{0}/footage.csv", stream_dir);

        if (File.Exists(footage_path)) {
            string[] footage_streams = File.ReadAllText(footage_path).Split(
                new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string footage_stream in footage_streams) {
                string footage_stream_trimmed = footage_stream.Trim();
                Texture2D[] stream_textures = new Texture2D[states_.Count];
                footage_.Add(footage_stream_trimmed, stream_textures);
                for (int i = 0; i < states_.Count; ++i) {
                    string file_name = string.Format("{0}/{1}_{2:D6}.png", stream_dir, footage_stream_trimmed, i);
                    try {
                        byte[] file_data = File.ReadAllBytes(file_name);
                        stream_textures[i] = new Texture2D(2, 2);
                        stream_textures[i].LoadImage(file_data);
                    } catch (FileNotFoundException) {
                        Logger.Warning("StateLoader::InitializeStateStream::Missing file: {0}.", file_name);
                        stream_textures[i] = Texture2D.blackTexture;
                    }
                }
            }
        }
        return true;
    }

    private JointController FindJoint(string joint_name) {
        JointController[] joints = GetComponentsInChildren<JointController>();
        foreach (JointController joint in joints) {
            if (joint_name.Equals(joint.gameObject.name)) {
                return joint;
            }
        }
        Logger.Warning("StateLoader::FindJoint::Could not find joint: {0}", joint_name);
        return null;
    }

    private Quaternion GetQuaternion(IList<float> values, int index) {
        // Quaternions in MuJoCo are in the wxyz form and unity needs xyzw.
        return new Quaternion(values[index + 1], values[index + 2], values[index + 3], values[index]);
    }

    private Quaternion GetQuaternion(float[] values, int index) {
        // Quaternions in MuJoCo are in the wxyz form and unity needs xyzw.
        return new Quaternion(values[index + 1], values[index + 2], values[index + 3], values[index]);
    }

    // In interactive mode go to the next state loaded from the file.
    public bool NextState() {
        if (states_.Count == 0) {
            return false;
        }

        current_frame_ = (current_frame_ + 1) % states_.Count;

        return UpdateState(states_[current_frame_]);
    }

    // In interactive mode go to the previous state loaded from the file.
    public bool PreviousState() {
        if (states_.Count == 0) {
            return false;
        }

        current_frame_ = (current_frame_ + states_.Count - 1) % states_.Count;

        return UpdateState(states_[current_frame_]);
    }

    // Update all joints from a list of qposes.
    public bool UpdateState(IList<float> state) {
        for (int i = 0; i < Mathf.Min(state.Count, mapping_.Length); ++i) {
            JointController joint = mapping_[i];
            if (joint != null) {
                switch (joint.joint_type_) {
                case JointController.JointType.Hinge:
                case JointController.JointType.Slide:
                    joint.UpdateJoint(state[i]);
                    break;
                case JointController.JointType.Ball:
                    joint.UpdateJoint(GetQuaternion(state, i));
                    break;
                }
            }
        }
        return true;
    }

    public void DrawEditorGUI() {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(" < ", GUILayout.Width(20))) {
            PreviousState();
        }
        if (GUILayout.Button(" > ", GUILayout.Width(20))) {
            NextState();
        }
        if (playing_ = GUILayout.Toggle(playing_, " Play ", GUILayout.Width(50))) {
            NextState();
        }
        GUILayout.Space(10);
        int new_frame = (int)GUILayout.HorizontalSlider(current_frame_, 0.0f,
                                                        states_.Count == 0 ? 0 : states_.Count - 1,
                                                        GUILayout.ExpandWidth(true));
        if (new_frame != current_frame_) {
            current_frame_ = new_frame;
            UpdateState(states_[current_frame_]);
        }

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperRight;
        GUILayout.Label(string.Format(" {0} / {1} ", current_frame_, states_.Count), style, GUILayout.Width(80));
        GUILayout.EndHorizontal();

        if (footage_.Count > 1) {
            RendererComponent.GUIHorizontalLine(1);
            GUILayout.BeginHorizontal();
            int button_size = Mathf.Min(100, 380 / (footage_.Count + 1));
            if (GUILayout.Button("Clear", GUILayout.Width(button_size), GUILayout.Height(button_size))) {
                reference_image_texture_ = null;
            }
            GUILayout.FlexibleSpace();
            foreach (KeyValuePair<string, Texture2D[]> streams in footage_) {
                if (streams.Value != null && streams.Value.Length > current_frame_) {
                    if (GUILayout.Button(streams.Value[current_frame_], GUILayout.Width(button_size),
                                         GUILayout.Height(button_size))) {
                        reference_image_texture_ = streams.Value[current_frame_];
                    }
                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            RendererComponent.GUISlider("overlay_alpha_", ref reference_overlay_alpha_, 0.0f, 1.0f);
            RendererComponent.GUIHorizontalLine(1);

        }
        GUILayout.EndVertical();
    }

    // Draw the transparent overlay with the image from the footage read from disk.
    public void DrawSceneGUI() {

        if (reference_image_texture_ != null) {
            overlay_material_.SetFloat("_Alpha", reference_overlay_alpha_);
            Graphics.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), reference_image_texture_,
                                 overlay_material_);
        }

    }
}
