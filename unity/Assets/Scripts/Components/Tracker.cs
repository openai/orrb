using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Tracker component produces auxiliary outputs that contain screen space positions of
// scene objects. For each tracked object 'object' and each camera 'camera' this component
// will emit a float output: 'tracker_object_X_camera' that contains 2 float values, i.e.:
// the normalized x and y screen coordinates of the tracked object.
//
// Configurable properties:
//   string camera_names - comma separated list of camera names to track objects with,
//   string tracked_object_names - comma separated list of objects to track,
//   string tracked_onbject_aliases - comma separated list of nice, human readable names
//                                    for the tracked objects.

public class Tracker : RendererComponent {

    [SerializeField]
    [ConfigProperty]
    public string camera_names_ = "";

    [SerializeField]
    [ConfigProperty]
    public string tracked_object_names_ = "";

    [SerializeField]
    [ConfigProperty]
    public string tracked_object_aliases_ = "";

    private List<GameObject> tracked_objects_ = new List<GameObject>();
    private string[] tracked_object_aliases_array_ = null;
    private List<Camera> cameras_ = new List<Camera>();
    private Texture marker_ = null;
    private Material overlay_material_ = null;
    private Texture2D bounding_box_texture_ = null;
    private GUIStyle bounding_box_style_ = null;

    // Cache the cameras for tracking and object to be tracked. 
    private void UpdateObjectsAndCameras() {
        string[] camera_names = camera_names_.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        string[] tracked_object_names = tracked_object_names_.Split(new char[] { ',' },
                                                                    StringSplitOptions.RemoveEmptyEntries);

        Camera[] cameras = GetComponentsInChildren<Camera>();

        foreach (Camera tracking_camera in cameras) {
            foreach (string camera_name in camera_names) {
                if (camera_name.Equals(tracking_camera.name)) {
                    cameras_.Add(tracking_camera);
                    break;
                }
            }
        }

        Transform[] children = GetComponentsInChildren<Transform>();

        foreach (Transform tracked_object in children) {
            foreach (string tracked_object_name in tracked_object_names) {
                if (tracked_object_name.Equals(tracked_object.name)) {
                    tracked_objects_.Add(tracked_object.gameObject);
                    break;
                }
            }
        }
    }

    public override bool InitializeComponent(Orrb.RendererComponentConfig config) {
        camera_names_ = ConfigUtils.GetProperty("camera_names", config, camera_names_);
        tracked_object_names_ = ConfigUtils.GetProperty("tracked_object_names", config, tracked_object_names_);
        tracked_object_aliases_array_ = tracked_object_aliases_.Split(new char[] { ',' },
                                                                      StringSplitOptions.RemoveEmptyEntries);

        // Load the crosshair texture and the overlay material, to be used in interactive mode.
        marker_ = Resources.Load<Texture>("Marker");
        overlay_material_ = new Material(Shader.Find("Unlit/Overlay"));

        // Prepare the bounding box style from a 9-sliced background texture.
        bounding_box_texture_ = Resources.Load<Texture2D>("BoundingBox");
        bounding_box_style_ = new GUIStyle();
        bounding_box_style_.normal.background = bounding_box_texture_;
        bounding_box_style_.border = new RectOffset(1, 1, 1, 1);

        UpdateObjectsAndCameras();
        return UpdateComponent(config);
    }

    public override bool UpdateComponent(Orrb.RendererComponentConfig config) {
        string old_camera_names = camera_names_;
        string old_tracked_object_names = tracked_object_names_;
        string old_tracked_object_aliases_ = tracked_object_aliases_;

        ConfigUtils.GetProperties(this, config);

        if (!camera_names_.Equals(old_camera_names) ||
            !tracked_object_names_.Equals(old_tracked_object_names)) {
            UpdateObjectsAndCameras();
        }

        if (!tracked_object_aliases_.Equals(old_tracked_object_aliases_)) {
            tracked_object_aliases_array_ = tracked_object_aliases_.Split(new char[] { ',' },
                                                                          StringSplitOptions.RemoveEmptyEntries);
        }
        return true;
    }

    private List<Vector3> GetAllCornersOfBounds(Bounds b) {
        // Take all the corner points in 3D.
        List<float> x_offsets = new List<float>() { -b.size.x / 2, b.size.x / 2 };
        List<float> y_offsets = new List<float>() { -b.size.y / 2, b.size.y / 2 };
        List<float> z_offsets = new List<float>() { -b.size.z / 2, b.size.z / 2 };

        List<Vector3> corners = new List<Vector3>();
        foreach (float x_offset in x_offsets) {
            foreach (float y_offset in y_offsets) {
                foreach (float z_offset in z_offsets) {
                    corners.Add(new Vector3(b.center.x + x_offset, b.center.y + y_offset, b.center.z + z_offset));
                }
            }
        }
        return corners;
    }

    private float[] GetBounds2DViewPoint(Camera tracking_camera, GameObject tracked_object, bool use_screen_point = false) {
        // Map the bounds of the `tracked_object` onto a 2D view of the `tracking_camera` by mapping 
        // all the corners and selecting the minimum / maximum on both x and y axises.
        // If use_screen_point = true, we map the corner points to screen; otherwise map to viewport.
        List<float> xs = new List<float>();
        List<float> ys = new List<float>();

        // Go through the bounds of each render associated with the object.
        foreach (Renderer render in tracked_object.GetComponentsInChildren<Renderer>()) {
            // Map all the corner points into the 2D view point or screen point.
            List<Vector3> corners = GetAllCornersOfBounds(render.bounds);

            foreach (Vector3 corner in corners) {
                Vector3 transformed_point;
                if (use_screen_point) {
                    transformed_point = tracking_camera.WorldToScreenPoint(corner);
                } else {
                    transformed_point = tracking_camera.WorldToViewportPoint(corner);
                }
                xs.Add(transformed_point.x);
                ys.Add(transformed_point.y);
            }
        }

        // Return the minimum and maximum values of x-axis and y-axis.
        return new float[] { xs.Min(), xs.Max(), ys.Min(), ys.Max() };
    }

    public override void DrawEditorGUI() {
        GUILayout.BeginVertical();
        foreach (Camera tracking_camera in cameras_) {
            GUILayout.Label(tracking_camera.name);
            for (int i = 0; i < tracked_objects_.Count; ++i) {
                GameObject tracked_object = tracked_objects_[i];
                string tracked_object_name = tracked_object.name;
                if (i < tracked_object_aliases_array_.Length) {
                    tracked_object_name = tracked_object_aliases_array_[i];
                }
                Vector3 viewport_position = tracking_camera.WorldToViewportPoint(tracked_object.transform.position);
                float[] bounds_2D = GetBounds2DViewPoint(tracking_camera, tracked_object);

                GUILayout.BeginHorizontal();
                GUILayout.Label(tracked_object.name, GUILayout.Width(200));
                GUILayout.Label(string.Format("x:{0:0.000###}", viewport_position.x), GUILayout.Width(100));
                GUILayout.Label(string.Format("y:{0:0.000###}", viewport_position.y), GUILayout.Width(100));
                GUILayout.EndHorizontal();

                GUILayout.Label("Bounds: " + string.Join(", ", bounds_2D));
                RendererComponent.GUIHorizontalLine(1);
            }
        }
        GUILayout.EndVertical();
    }

    // Draw the tracking crosshair on the overlay, in interactive mode.
    public override void DrawSceneGUI() {
        foreach (Camera tracking_camera in cameras_) {
            if (tracking_camera.Equals(Camera.current)) {
                foreach (GameObject tracked_object in tracked_objects_) {
                    Vector3 screen_position = tracking_camera.WorldToScreenPoint(tracked_object.transform.position);
                    Graphics.DrawTexture(new Rect(screen_position.x - 5, Screen.height - screen_position.y - 5, 10, 10), marker_, overlay_material_);

                    float[] bounds = GetBounds2DViewPoint(tracking_camera, tracked_object, true);
                    float y_min = Screen.height - bounds[3];
                    float y_max = Screen.height - bounds[2];
                    float height = y_max - y_min;
                    float width = bounds[1] - bounds[0];
                    GUI.Box(new Rect(bounds[0], y_min, width, height), "", bounding_box_style_);
                }
            }
        }
    }

    public override bool RunComponent(IOutputContext context) {
        foreach (Camera tracking_camera in cameras_) {
            for (int i = 0; i < tracked_objects_.Count; ++i) {
                GameObject tracked_object = tracked_objects_[i];
                string tracked_object_name = tracked_object.name;
                if (i < tracked_object_aliases_array_.Length) {
                    // If there is an alias on the alias list, use it.
                    tracked_object_name = tracked_object_aliases_array_[i];
                }

                // Return the center of the object.
                string stream_name = string.Format("tracker_{0}_X_{1}", tracked_object_name, tracking_camera.name);
                Vector3 viewport_position = tracking_camera.WorldToViewportPoint(tracked_object.transform.position);
                context.OutputFloats(stream_name, new float[] { viewport_position.x, viewport_position.y });

                // Return the bounding box
                string stream_name_bbox = string.Format("tracker_{0}_X_{1}_bbox", tracked_object_name, tracking_camera.name);
                context.OutputFloats(stream_name_bbox, GetBounds2DViewPoint(tracking_camera, tracked_object));
            }
        }
        return true;
    }
}
