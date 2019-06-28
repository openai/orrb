using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class BuildUtils : EditorWindow {

    public enum SupportedTarget {
        Linux64 = BuildTarget.StandaloneLinux64,
        MacOS64 = BuildTarget.StandaloneOSX,
        All
    };

    private string name_ = "";
    private string version_ = "";
    private SupportedTarget target_ = SupportedTarget.All;
    private string scene_ = "";
    private bool development_build_ = false;

    private static string GetArgument(string argument, string default_value) {
        string plus_argument = string.Format("+{0}", argument);
        string[] commandline_args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < commandline_args.Length - 1; ++i) {
            if (commandline_args[i].Equals(plus_argument)) {
                return commandline_args[i + 1];
            }
        }
        return default_value;
    }

    private static BuildTarget GetTarget() {
        string target = GetArgument("target", "Linux-x64_86");
        if (target.Equals("Linux-x86_64")) {
            return BuildTarget.StandaloneLinux64;
        } else if (target.Equals("Darwin-x86_64")) {
            return BuildTarget.StandaloneOSX;
        } else {
            return BuildTarget.StandaloneLinux64;
        }
    }

    private static string GetLocationDir(string name, BuildTarget target, string version) {
        if (target == BuildTarget.StandaloneLinux64) {
            return string.Format("Builds/{0}-Linux-x86_64-{1}/", name, version);
        } else if (target == BuildTarget.StandaloneOSX) {
            return string.Format("Builds/{0}-Darwin-x86_64-{1}/", name, version);
        } else {
            return string.Format("Builds/{0}-{1}/", name, version);
        }
    }

    private static string GetBinary(string location_dir, string name, BuildTarget target) {
        if (target == BuildTarget.StandaloneLinux64) {
            return string.Format("{0}{1}.x86_64", location_dir, name);
        } else if (target == BuildTarget.StandaloneOSX) {
            return string.Format("{0}{1}.app", location_dir, name);
        } else {
            return string.Format("{0}{1}", location_dir, name);
        }
    }

    static void BuildCommandline() {
        BuildTarget target = GetTarget();
        string name = GetArgument("name", "StandaloneRenderer");
        string version = GetArgument("version", System.DateTime.Now.ToString("yyyyMMdd"));
        string scene = GetArgument("scene", "Assets/Scenes/StandaloneRenderer.unity");
        string development_build = GetArgument("devel", "false");

        BuildOneTarget(target, scene, name, version, "true".Equals(development_build));
    }

    [MenuItem("GPR/Build")]
    static void BuildMenu() {
        BuildUtils build_utils = ScriptableObject.CreateInstance<BuildUtils>();
        build_utils.titleContent = new GUIContent("GPR Build Utils");
        build_utils.position = new Rect(Screen.width / 2, Screen.height / 2, 600, 100);
        build_utils.minSize = new Vector2(600, 100);
        build_utils.maxSize = new Vector2(600, 100);
        build_utils.name_ = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        build_utils.scene_ = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        build_utils.version_ = System.DateTime.Now.ToString("yyyyMMdd");
        build_utils.ShowUtility();
    }

    private static void BuildOneTarget(BuildTarget target, string scene, string name, string version,
                                       bool development_build) {
        string directory = GetLocationDir(name, target, version);
        string binary = GetBinary(directory, name, target);

        System.Console.WriteLine(string.Format("Building: {0}", binary));

        Directory.CreateDirectory(directory);
        BuildPlayerOptions options = new BuildPlayerOptions();
        if (development_build) {
            options.options = BuildOptions.Development;
        } else {
            options.options = BuildOptions.None;
        }
        options.scenes = new string[] { scene };
        options.target = target;
        options.locationPathName = binary;

        BuildPipeline.BuildPlayer(options);
    }

    private static void BuildAndPush(SupportedTarget target, string scene, string name, string version,
                                     bool development_build) {
        if (target == SupportedTarget.All) {
            BuildOneTarget(BuildTarget.StandaloneLinux64, scene, name, version, development_build);
            BuildOneTarget(BuildTarget.StandaloneOSX, scene, name, version, development_build);
        } else {
            BuildOneTarget((BuildTarget)target, scene, name, version, development_build);
        }
    }

    void OnGUI() {
        EditorGUILayout.BeginVertical();
        name_ = EditorGUILayout.TextField("name", name_);
        version_ = EditorGUILayout.TextField("version", version_);
        target_ = (SupportedTarget)EditorGUILayout.EnumPopup("target", target_);
        scene_ = EditorGUILayout.TextField("scene", scene_);
        development_build_ = EditorGUILayout.Toggle("development build", development_build_);

        bool should_build = false;

        if (GUILayout.Button("Build")) {
            should_build = true;
        }

        EditorGUILayout.EndVertical();

        if (should_build) {
            BuildAndPush(target_, scene_, name_, version_, development_build_);
        }
    }

}
