using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using System.IO;
using Parabox.STL;

// This monstrosity loads a MuJoCo XML and rebuilds the robot.
// It loads the required geoms from STL files and generates the
// geometric primitives necessary. It recreates the joints, geometry,
// cameras and sites defined in the XML. MuJoCo class inheritance
// is supported for those entities. Far from everything in the MuJoCo
// modelling reference is implemented.

public class RobotLoader : KineticHierarchyController {

    public struct MeshDefinition {
        public string name;
        public Vector3 scale;
        public byte[] contents;

        public MeshDefinition(string name, Vector3 scale, byte[] contents) {
            this.name = name;
            this.scale = scale;
            this.contents = contents;
        }
    }

    public struct TextureDefinition {
        public string name;
        public byte[] contents;

        public TextureDefinition(string name, byte[] contents) {
            this.name = name;
            this.contents = contents;
        }
    }

    [SerializeField]
    public GameObject assembly_parts_ = null;

    [SerializeField]
    public Material default_material_ = null;

    [SerializeField]
    public bool convexify_meshes_ = false;

    private Dictionary<string, Material> materials_ = new Dictionary<string, Material>();

    private int part_counter_ = 0;
    private int camera_counter_ = 0;

    private string asset_basedir_ = ".";
    private string mesh_dir_ = ".";
    private string texture_dir_ = ".";

    private List<string> geom_categories_ = new List<string>();

    private GameObject mr_robot_ = null;

    // Main entry point, use the provided directory as base for
    // relative paths.
    public bool LoadRobot(string xml_file, string asset_basedir) {
        return LoadRobot(xml_file, asset_basedir, new List<TextureDefinition>(), new List<MeshDefinition>());
    }

    public bool LoadRobot(string xml_file, string asset_basedir, IList<TextureDefinition> textures,
                          IList<MeshDefinition> meshes) {
        this.asset_basedir_ = asset_basedir;
        xml_file = ConfigUtils.ResolveFile(asset_basedir_, xml_file);

        XmlDocument mr_robot_xml = new XmlDocument();
        mr_robot_xml.Load(xml_file);

        // If the XML has includes, merge everything into one.
        ResolveXmlIncludes(Path.GetDirectoryName(xml_file), mr_robot_xml);

        XmlNode mujoco = XmlUtils.GetChildNode(mr_robot_xml, "mujoco");

        if (mujoco == null) {
            Logger.Error("RobotLoader::LoadRobot::Cannot find mujoco node.");
            return false;
        }

        // Set up global properties.
        HandleCompilerDirectives(mujoco);

        // Preload textures and geom meshes.
        if (!PrepareAssets(mr_robot_xml, textures, meshes)) {
            Logger.Error("RobotLoader::LoadRobot::Failed to prepare assets.");
            return false;
        }

        XmlUtils.Defaults defaults = new XmlUtils.Defaults(XmlUtils.GetChildNode(mujoco, "default"));
        XmlNode worldbody = XmlUtils.GetChildNode(mujoco, "worldbody");

        if (worldbody == null) {
            Logger.Error("RobotLoader::LoadRobot::No worldbody defined.");
            return false;
        }

        // Prepare geom names for semantic segmentation
        PrepareGeomCategories(worldbody);
        geom_categories_.Sort();

        if (!AddRobotBody(this, worldbody, defaults)) {
            Logger.Error("RobotLoader::LoadRobot::Failed to build robot parts.");
            return false;
        }

        name = "robot";

        // MuJoCo has a different world orientation than Unity.
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);

        return true;
    }

    private void PrepareGeomCategories(XmlNode node) {
        List<XmlNode> geom_nodes = XmlUtils.GetChildNodes(node, "geom");
        foreach (XmlNode geom_node in geom_nodes) {
            string geom_category = GeomController.GetGeomCategoryFromXml(geom_node);

            if (geom_category != null && !geom_categories_.Contains(geom_category)) {
                geom_categories_.Add(geom_category);
            }

        }
        List<XmlNode> body_nodes = XmlUtils.GetChildNodes(node, "body");
        foreach (XmlNode body_node in body_nodes) {
            PrepareGeomCategories(body_node);
        }
    }

    private void HandleCompilerDirectives(XmlNode node) {
        foreach (XmlNode child_node in node.ChildNodes) {
            HandleCompilerDirectives(child_node);
        }

        List<XmlNode> compiler_directives = XmlUtils.GetChildNodes(node, "compiler");

        foreach (XmlNode compiler_directive in compiler_directives) {
            mesh_dir_ = XmlUtils.GetString(compiler_directive, "meshdir", mesh_dir_);
            texture_dir_ = XmlUtils.GetString(compiler_directive, "texturedir", texture_dir_);
        }
    }

    private bool PrepareAssets(XmlNode node, IList<TextureDefinition> textures,
                               IList<MeshDefinition> meshes) {
        // Load assets that were passed directly in code.
        foreach (MeshDefinition mesh in meshes) {
            if (!LoadMeshDefinition(mesh)) {
                return false;
            }
        }

        foreach (TextureDefinition texture in textures) {
            if (!LoadTextureDefinition(texture)) {
                return false;
            }
        }

        // Load assets that are defined in the XML.
        return PrepareXmlAssets(node);
    }

    private bool LoadMeshDefinition(MeshDefinition mesh) {
        List<Mesh> meshes = pb_Stl_Importer.ImportBytes(mesh.contents);

        if (meshes == null) {
            Logger.Error("RobotLoader::LoadMeshDefinition::Cannot load: {} from MeshDefinition contents.", mesh.name);
            return false;
        }

        if (meshes.Count == 0) {
            Logger.Error("RobotLoader::LoadMeshDefinition::No meshes in MeshDefinition for: {}.", mesh.name);
            return false;
        }

        return CreateMeshAsset(assembly_parts_, mesh.name, mesh.scale, meshes);
    }

    private bool LoadTextureDefinition(TextureDefinition texture) {
        Logger.Warning("RobotLoader::LoadTextureDefinition::Ignoring texture: {0}.", texture.name);
        return true;
    }

    private bool PrepareXmlAssets(XmlNode node) {
        foreach (XmlNode child_node in node.ChildNodes) {
            if (!PrepareXmlAssets(child_node)) {
                return false;
            }
        }

        // Look in the <asset></asset> tag.
        List<XmlNode> asset_nodes = XmlUtils.GetChildNodes(node, "asset");

        foreach (XmlNode asset_node in asset_nodes) {
            List<XmlNode> mesh_nodes = XmlUtils.GetChildNodes(asset_node, "mesh");
            foreach (XmlNode mesh_node in mesh_nodes) {
                if (!LoadXmlMeshAsset(mesh_node)) {
                    return false;
                }
            }

            List<XmlNode> material_nodes = XmlUtils.GetChildNodes(asset_node, "material");
            foreach (XmlNode material_node in material_nodes) {
                if (!LoadXmlMaterialAsset(material_node)) {
                    return false;
                }
            }
        }
        return true;
    }

    private bool LoadXmlMaterialAsset(XmlNode material_node) {
        string material_name = XmlUtils.GetString(material_node, "name", null);

        if (material_name == null) {
            Logger.Error("RobotLoader::LoadXmlMaterialAsset::Missing material name.");
            return false;
        }

        Material material = new Material(default_material_);
        material.name = material_name;

        if (material.HasProperty("_Color")) {
            material.color = XmlUtils.GetColor(material_node, "rgba", Color.grey);
        }

        if (material.HasProperty("_SpecColor")) {
            material.SetColor("_SpecColor", XmlUtils.GetColor(material_node, "specular_rgba", Color.grey));
        }

        if (material.HasProperty("_Glossiness")) {
            material.SetFloat("_Glossiness", XmlUtils.GetFloat(material_node, "shininess", 0.5f));
        }

        if (material.HasProperty("_Metallic")) {
            material.SetFloat("_Metallic", XmlUtils.GetFloat(material_node, "specular", 0.5f));
        }

        // If the XML contains the emission parameter for this material,
        // turn emissive color on.
        float emission = XmlUtils.GetFloat(material_node, "emission", 0.0f);
        if (emission > 0.001f && material.HasProperty("_EmissionColor")) {
            Color finalEmission = emission * XmlUtils.GetColor(material_node, "rgba", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", finalEmission);
        }

        materials_.Add(material_name, material);
        return true;
    }

    private bool LoadXmlMeshAsset(XmlNode mesh_node) {
        string mesh_name = XmlUtils.GetString(mesh_node, "name", null);

        if (mesh_name == null) {
            Logger.Error("RobotLoader::LoadXmlMeshAsset::Missing mesh name.");
            return false;
        }

        string mesh_file = XmlUtils.GetString(mesh_node, "file", null);

        if (mesh_file == null) {
            Logger.Error("RobotLoader::LoadXmlMeshAsset::Missing mesh file for: {}.", mesh_name);
            return false;
        }

        string mesh_path = ConfigUtils.ResolveFile(asset_basedir_, Path.Combine(mesh_dir_, mesh_file));

        // Import STL for this mesh. Unity has a 64k vertices per mesh
        // limit (in order to fit indexes in short) so one STL might
        // produce multiple Meshes.
        IList<Mesh> meshes = pb_Stl_Importer.Import(mesh_path);

        // We can globaly turn convexification on. MuJoCo convexifies all
        // geoms for collision detection, this mode can help debug / inspect it.
        if (convexify_meshes_) {
            List<Mesh> convex_meshes = new List<Mesh>();
            foreach (Mesh mesh in meshes) {
                convex_meshes.Add(ConvexHull.CreateConvexHull(mesh));
            }
            meshes = convex_meshes;
        }

        if (meshes == null) {
            Logger.Error("RobotLoader::LoadXmlMeshAsset::Cannot load: {} from: {}.", mesh_name, mesh_path);
            return false;
        }

        if (meshes.Count == 0) {
            Logger.Error("RobotLoader::LoadXmlMeshAsset::No meshes in: {} for: {}.", mesh_path, mesh_name);
            return false;
        }

        Vector3 scale = XmlUtils.GetVector3(mesh_node, "scale", Vector3.one);

        return CreateMeshAsset(assembly_parts_, mesh_name, scale, meshes);
    }

    // Take the mesh and its name and put the prefab asset in
    // a proper place in the prefab hierarchy.
    private bool CreateMeshAsset(GameObject parent, string asset_name, Vector3 scale, IList<Mesh> meshes) {
        string[] split = asset_name.Split(new char[] { ':' }, 2, System.StringSplitOptions.RemoveEmptyEntries);

        Transform asset_prefab_transform = parent.transform.Find(split[0]);
        GameObject asset_prefab = null;

        if (asset_prefab_transform != null) {
            asset_prefab = asset_prefab_transform.gameObject;
        }

        if (asset_prefab == null) {
            asset_prefab = new GameObject(split[0]);
            asset_prefab.transform.parent = parent.transform;
            asset_prefab.transform.localPosition = Vector3.zero;
            asset_prefab.transform.localRotation = Quaternion.identity;
            asset_prefab.transform.localScale = Vector3.one;
            asset_prefab.AddComponent<GeomController>();
        }

        if (split.Length == 2) {
            return CreateMeshAsset(asset_prefab, split[1], scale, meshes);
        } else {
            asset_prefab.transform.localScale = scale;

            int counter = 0;
            foreach (Mesh mesh in meshes) {
                GameObject asset_mesh = new GameObject(string.Format("{0}_mesh_{1}", asset_name, counter++));
                asset_mesh.transform.parent = asset_prefab.transform;
                asset_mesh.transform.localPosition = Vector3.zero;
                asset_mesh.transform.localRotation = Quaternion.identity;
                asset_mesh.transform.localScale = Vector3.one;
                asset_mesh.AddComponent<MeshFilter>().sharedMesh = mesh;
                asset_mesh.AddComponent<MeshRenderer>().sharedMaterial = default_material_;
            }

            return true;
        }
    }

    private void ResolveXmlIncludes(string root_dir, XmlNode node) {
        foreach (XmlNode child in node.ChildNodes) {
            ResolveXmlIncludes(root_dir, child);
        }

        List<XmlNode> includes = XmlUtils.GetChildNodes(node, "include");

        foreach (XmlNode include in includes) {
            XmlDocument loaded_include = LoadInclude(root_dir, include);

            if (loaded_include == null) {
                Logger.Error("RobotLoader::ResolveXMLIncludes::Cannot load include.");
                continue;
            }

            node.RemoveChild(include);

            XmlNode mujoco_root_node = XmlUtils.GetChildNode(loaded_include, "mujocoinclude");

            if (mujoco_root_node == null) {
                Logger.Error("RobotLoader::ResolveXmlIncludes::Included file needs a mujocoinclude node.");
                continue;
            }

            foreach (XmlNode included_child in mujoco_root_node.ChildNodes) {
                XmlNode imported_child = node.OwnerDocument.ImportNode(included_child, true);
                node.AppendChild(imported_child);
            }
        }
    }

    private XmlDocument LoadInclude(string root_dir, XmlNode include_node) {
        string file = XmlUtils.GetString(include_node, "file", null);

        if (file == null) {
            Logger.Error("RobotLoader::LoadInclude::Missing file attribute in include node.");
            return null;
        }

        XmlDocument included_document = new XmlDocument();
        included_document.Load(Path.Combine(root_dir, file));
        ResolveXmlIncludes(root_dir, included_document);
        return included_document;
    }

    // Get the XML with a default class for a given entity class_name.
    private XmlNode FindDefault(XmlNode node, string class_name) {
        List<XmlNode> default_nodes = XmlUtils.GetChildNodes(node, "default");
        foreach (XmlNode default_node in default_nodes) {
            string default_class_name = XmlUtils.GetString(default_node, "class", null);
            if (class_name.Equals(default_class_name)) {
                return default_node;
            }
        }
        return null;
    }

    // Handle creation of a MuJoCo XML body / worldbody entity.
    private bool AddRobotBody(KineticHierarchyController parent, XmlNode part_xml, XmlUtils.Defaults defaults) {
        defaults = defaults.Resolve(XmlUtils.GetString(part_xml, "childclass", null));
        KineticHierarchyController robot_part_attachment = BuildRobotBodyAttachment(parent, part_xml, defaults);
        return BuildRobotBodyChildren(robot_part_attachment, part_xml, defaults);
    }

    // Create the hierarchy that handles local transformations
    // and joints for a given body.
    private KineticHierarchyController BuildRobotBodyAttachment(KineticHierarchyController parent, XmlNode part_xml,
                                                                XmlUtils.Defaults defaults) {
        string part_name = XmlUtils.GetString(part_xml, "name", string.Format("part_{0}", part_counter_));
        part_counter_++;

        // Build the body and set local position/rotation/scale relative to parent.
        BodyController body = SceneUtils.InstantiateWithController<BodyController>(part_name);
        body.Initialize(parent, part_name, XmlUtils.GetVector3(part_xml, "pos", Vector3.zero),
                        XmlUtils.GetRotation(part_xml, Quaternion.identity));


        List<XmlNode> joint_nodes = XmlUtils.GetChildNodes(part_xml, "joint");

        // Add all the joints in a hierarchy, one after another
        // (XML order is important).
        KineticHierarchyController last_game_object = body;
        for (int i = 0; i < joint_nodes.Count; ++i) {
            last_game_object = BuildJoint(last_game_object, part_name, i, joint_nodes[i], defaults);
        }
        return last_game_object;
    }

    // Recursively add geoms, cameras, sites and other bodies.
    private bool BuildRobotBodyChildren(KineticHierarchyController parent, XmlNode part_xml,
                                        XmlUtils.Defaults defaults) {
        List<XmlNode> geom_nodes = XmlUtils.GetChildNodes(part_xml, "geom");
        foreach (XmlNode geom_node in geom_nodes) {
            if (!AddRobotGeom(parent, geom_node, defaults)) {
                Logger.Error("RobotLoader::BuildRobotBodyChildren::Cannot add robot geom.");
                return false;
            }
        }

        List<XmlNode> camera_nodes = XmlUtils.GetChildNodes(part_xml, "camera");
        foreach (XmlNode camera_node in camera_nodes) {
            if (!AddCamera(parent, camera_node, defaults)) {
                Logger.Error("RobotLoader::BuildRobotBodyChildren::Cannot add robot camera.");
                return false;
            }
        }

        List<XmlNode> site_nodes = XmlUtils.GetChildNodes(part_xml, "site");
        foreach (XmlNode site_node in site_nodes) {
            if (!AddRobotSite(parent, site_node, defaults)) {
                Logger.Error("RobotLoader::BuildRobotBodyChildren::Cannot add robot site.");
                return false;
            }
        }

        List<XmlNode> child_nodes = XmlUtils.GetChildNodes(part_xml, "body");
        foreach (XmlNode child_node in child_nodes) {
            if (!AddRobotBody(parent, child_node, defaults)) {
                Logger.Error("RobotLoader::BuildRobotBodyChildren::Cannot add robot body.");
                return false;
            }
        }

        return true;
    }

    private KineticHierarchyController BuildJoint(KineticHierarchyController parent, string part_name, int id,
                                                  XmlNode joint_xml, XmlUtils.Defaults defaults) {
        string joint_name = XmlUtils.GetString(joint_xml, "name", string.Format("{0}_joint_{1}", part_name, id));

        XmlUtils.Defaults joint_defaults = defaults.GetSubclass("joint");

        JointController joint = SceneUtils.InstantiateWithController<JointController>(joint_name);
        joint.Initialize(parent, joint_name,
                         XmlUtils.GetVector3WithDefaults(joint_xml, joint_defaults, "pos", Vector3.zero),
                         XmlUtils.GetRotationWithDefaults(joint_xml, joint_defaults, Quaternion.identity),
                         XmlUtils.GetVector3WithDefaults(joint_xml, joint_defaults, "axis", Vector3.up),
                         XmlUtils.GetVector2WithDefaults(joint_xml, joint_defaults, "range",
                                                         new Vector2(float.MinValue, float.MaxValue)),
                         XmlUtils.GetStringWithDefaults(joint_xml, joint_defaults, "type", null));

        return joint;
    }

    private bool AddCamera(KineticHierarchyController parent, XmlNode camera_xml, XmlUtils.Defaults defaults) {
        string camera_name = XmlUtils.GetString(camera_xml, "name", null);
        if (camera_name == null) {
            camera_name = string.Format("camera_{0}", camera_counter_++);
        }

        Camera camera_prototype = SceneUtils.Find<Camera>(assembly_parts_, camera_name,
                                                     SceneUtils.Find<Camera>(assembly_parts_, "__camera_template", null));

        if (camera_prototype == null) {
            Logger.Error("RobotLoader::AddCamera::Cannot find camera prefab for: {0}", camera_name);
            return false;
        }

        Camera robot_camera = Instantiate(camera_prototype);
        robot_camera.name = camera_name;
        robot_camera.transform.parent = parent.transform;
        robot_camera.transform.localRotation = XmlUtils.GetRotation(camera_xml, Quaternion.identity);
        robot_camera.transform.localPosition = XmlUtils.GetVector3(camera_xml, "pos", Vector3.zero);
        robot_camera.transform.LookAt(robot_camera.transform.position + robot_camera.transform.TransformDirection(Vector3.back),
                                      robot_camera.transform.TransformDirection(Vector3.up));
        robot_camera.fieldOfView = XmlUtils.GetFloat(camera_xml, "fovy", robot_camera.fieldOfView);

        return true;
    }

    public GameObject GetRobot() {
        return mr_robot_;
    }

    // Get the camera template, that will be cloned when instantiating
    // cameras in the XML.
    private GameObject FindCameraPrefab(string prefab_name) {
        for (int i = 0; i < assembly_parts_.transform.childCount; ++i) {
            if (assembly_parts_.transform.GetChild(i).name == prefab_name) {
                return assembly_parts_.transform.GetChild(i).gameObject;
            }
        }

        if (prefab_name == "__camera_template") {
            Logger.Warning("RobotLoader::FindCameraPrefab::Cannot find default camera.");
            return null;
        } else {
            Logger.Warning("RobotLoader::FindCameraPrefab::Cannot find {0}, looking for default.", prefab_name);
            return FindCameraPrefab("__camera_template");
        }
    }

    private bool AddRobotSite(KineticHierarchyController parent, XmlNode site_xml, XmlUtils.Defaults defaults) {
        defaults = defaults.Resolve(XmlUtils.GetString(site_xml, "class", null)).GetSubclass("site");

        SiteController site = null;

        string type = XmlUtils.GetStringWithDefaults(site_xml, defaults, "type", "sphere");

        if ("box".Equals(type)) {
            site = AddRobotBoxSite(parent, site_xml, defaults);
        } else if ("plane".Equals(type)) {
            site = AddRobotPlaneSite(parent, site_xml, defaults);
        } else if (type == null || "sphere".Equals(type)) {
            site = AddRobotSphereSite(parent, site_xml, defaults);
        } else if ("cylinder".Equals(type)) {
            site = AddRobotCylinderSite(parent, site_xml, defaults);
        } else if ("capsule".Equals(type)) {
            site = AddRobotCapsuleSite(parent, site_xml, defaults);
        } else {
            Logger.Error("RobotLoader::AddRobotSite::Unsupported site type: {0} in {1}.", type, site_xml.OuterXml);
            return false;
        }

        if (site == null) {
            Logger.Error("RobotLoader::AddRobotSite::Cannot instantiate site.");
            return false;
        }

        ResolveMaterial(site_xml, defaults, site);
        return true;
    }

    private SiteController AddRobotBoxSite(KineticHierarchyController parent, XmlNode site_xml,
                                           XmlUtils.Defaults defaults) {
        return SiteController.CreateBox(
            parent,
            XmlUtils.GetString(site_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(site_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(site_xml, defaults, Quaternion.identity),
            2 * XmlUtils.GetVector3WithDefaults(site_xml, defaults, "size", Vector3.one));
    }

    private SiteController AddRobotSphereSite(KineticHierarchyController parent, XmlNode site_xml,
                                              XmlUtils.Defaults defaults) {
        Vector3 size = XmlUtils.GetVector3WithDefaults(site_xml, defaults, "size", Vector3.zero);
        return SiteController.CreateSphere(
            parent,
            XmlUtils.GetString(site_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(site_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(site_xml, defaults, Quaternion.identity),
            Vector3.one * 2.0f * size.x);
    }

    private SiteController AddRobotPlaneSite(KineticHierarchyController parent, XmlNode site_xml,
                                             XmlUtils.Defaults defaults) {
        Vector3 mujoco_size = XmlUtils.GetVector3WithDefaults(site_xml, defaults, "size", Vector3.one);
        return SiteController.CreatePlane(
            parent,
            XmlUtils.GetString(site_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(site_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(site_xml, defaults, Quaternion.identity)
                * Quaternion.Euler(90.0f, 0.0f, 0.0f),
            2 * new Vector3(mujoco_size.x / 10.0f, 0.0f, mujoco_size.y / 10.0f));
    }

    private SiteController AddRobotCylinderSite(KineticHierarchyController parent, XmlNode site_xml,
                                                XmlUtils.Defaults defaults) {
        Vector2 mujoco_size = XmlUtils.GetVector2WithDefaults(site_xml, defaults, "size", Vector2.one);
        return SiteController.CreateCylinder(
            parent,
            XmlUtils.GetString(site_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(site_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(site_xml, defaults, Quaternion.identity),
            2 * new Vector3(mujoco_size.x, mujoco_size.y / 2.0f, mujoco_size.x));
    }

    // A capsule can be defined with a from-to pair, which
    // complicates the code a little.
    private SiteController AddRobotCapsuleSite(KineticHierarchyController parent, XmlNode site_xml,
                                               XmlUtils.Defaults defaults) {
        string capsule_name = XmlUtils.GetString(site_xml, "name", null);
        if (XmlUtils.HasAttribute(site_xml, "fromto")) {
            string from_to = XmlUtils.GetStringWithDefaults(site_xml, defaults, "fromto", "0 0 0 0 0 0");
            string[] from_to_split = from_to.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (from_to_split.Length != 6) {
                Logger.Error("RobotLoader::AddRobotCapsuleGeom::Malformed fromto: {0}", from_to);
                return null;
            }
            Vector3 from_position = new Vector3(float.Parse(from_to_split[0]), float.Parse(from_to_split[1]),
                                                float.Parse(from_to_split[2]));
            Vector3 to_position = new Vector3(float.Parse(from_to_split[3]), float.Parse(from_to_split[4]),
                                              float.Parse(from_to_split[5]));
            Vector3 center = (from_position + to_position) / 2.0f;
            float half_length = (to_position - from_position).magnitude / 2.0f;
            Quaternion rotation = Quaternion.LookRotation(to_position - from_position);
            return SiteController.CreateCapsule(
                parent, capsule_name, center, rotation, half_length, XmlUtils.GetFloat(site_xml, "size", 1.0f));
        } else {
            Vector2 mujoco_size = XmlUtils.GetVector2WithDefaults(site_xml, defaults, "size", Vector2.one);
            return SiteController.CreateCapsule(
                parent, capsule_name,
                XmlUtils.GetVector3WithDefaults(site_xml, defaults, "pos", Vector3.zero),
                XmlUtils.GetRotationWithDefaults(site_xml, defaults, Quaternion.identity),
                mujoco_size.y,
                mujoco_size.x);
        }
    }

    private bool AddRobotGeom(KineticHierarchyController parent, XmlNode geom_xml, XmlUtils.Defaults defaults) {
        defaults = defaults.Resolve(XmlUtils.GetString(geom_xml, "class", null)).GetSubclass("geom");

        GeomController geom = null;

        string mesh_name = XmlUtils.GetStringWithDefaults(geom_xml, defaults, "mesh", null);
        string type = XmlUtils.GetStringWithDefaults(geom_xml, defaults, "type", null);

        if (mesh_name != null) {
            geom = AddRobotMeshGeom(parent, geom_xml, defaults, mesh_name);
        } else if ("box".Equals(type)) {
            geom = AddRobotBoxGeom(parent, geom_xml, defaults);
        } else if ("plane".Equals(type)) {
            geom = AddRobotPlaneGeom(parent, geom_xml, defaults);
        } else if (type == null || "sphere".Equals(type)) {
            geom = AddRobotSphereGeom(parent, geom_xml, defaults);
        } else if ("cylinder".Equals(type)) {
            geom = AddRobotCylinderGeom(parent, geom_xml, defaults);
        } else if ("capsule".Equals(type)) {
            geom = AddRobotCapsuleGeom(parent, geom_xml, defaults);
        } else {
            Logger.Error("RobotLoader::AddRobotGeom::Unsupported geom type: {0} in {1}.", type, geom_xml.OuterXml);
            return false;
        }

        if (geom == null) {
            Logger.Error("RobotLoader::AddRobotGeom::Cannot instantiate geom.");
            return false;
        }

        // Set the geom category for semantic segmentation
        UpdateGeomCategory(geom, geom_xml);

        // Find the material in the preloaded assets.
        ResolveMaterial(geom_xml, defaults, geom);
        return true;
    }

    private void UpdateGeomCategory(GeomController geom, XmlNode geom_xml) {
        string geom_category = GeomController.GetGeomCategoryFromXml(geom_xml);
        if (geom_category != null) {
            // the background has category id = 0, hence the +1
            geom.category_id_ = geom_categories_.IndexOf(geom_category) + 1;
            geom.SetCategoryRendererProperties();
        }
    }

    private GeomController AddRobotBoxGeom(KineticHierarchyController parent, XmlNode geom_xml,
                                           XmlUtils.Defaults defaults) {
        return GeomController.CreateBox(
            parent,
            XmlUtils.GetString(geom_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(geom_xml, defaults, Quaternion.identity),
            2 * XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "size", Vector3.one));
    }

    private GeomController AddRobotSphereGeom(KineticHierarchyController parent, XmlNode geom_xml,
                                              XmlUtils.Defaults defaults) {
        return GeomController.CreateSphere(
            parent,
            XmlUtils.GetString(geom_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(geom_xml, defaults, Quaternion.identity),
            2 * XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "size", Vector3.one));
    }

    private GeomController AddRobotPlaneGeom(KineticHierarchyController parent, XmlNode geom_xml,
                                             XmlUtils.Defaults defaults) {
        Vector3 mujoco_size = XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "size", Vector3.one);
        return GeomController.CreatePlane(
            parent,
            XmlUtils.GetString(geom_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(geom_xml, defaults, Quaternion.identity)
                * Quaternion.Euler(90.0f, 0.0f, 0.0f),
            2 * new Vector3(mujoco_size.x / 10.0f, 0.0f, mujoco_size.y / 10.0f));
    }

    private GeomController AddRobotCylinderGeom(KineticHierarchyController parent, XmlNode geom_xml,
                                                XmlUtils.Defaults defaults) {
        Vector2 mujoco_size = XmlUtils.GetVector2WithDefaults(geom_xml, defaults, "size", Vector2.one);
        return GeomController.CreateCylinder(
            parent,
            XmlUtils.GetString(geom_xml, "name", null),
            XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "pos", Vector3.zero),
            XmlUtils.GetRotationWithDefaults(geom_xml, defaults, Quaternion.identity),
            2 * new Vector3(mujoco_size.x, mujoco_size.y / 2.0f, mujoco_size.x));
    }

    // A capsule can be defined with a from-to pair, which complicates
    // the code a little.
    private GeomController AddRobotCapsuleGeom(KineticHierarchyController parent, XmlNode geom_xml,
                                               XmlUtils.Defaults defaults) {
        string capsule_name = XmlUtils.GetString(geom_xml, "name", null);
        if (XmlUtils.HasAttribute(geom_xml, "fromto")) {
            string from_to = XmlUtils.GetStringWithDefaults(geom_xml, defaults, "fromto", "0 0 0 0 0 0");
            string[] from_to_split = from_to.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (from_to_split.Length != 6) {
                Logger.Error("RobotLoader::AddRobotCapsuleGeom::Malformed fromto: {0}", from_to);
                return null;
            }
            Vector3 from_position = new Vector3(float.Parse(from_to_split[0]), float.Parse(from_to_split[1]),
                                                float.Parse(from_to_split[2]));
            Vector3 to_position = new Vector3(float.Parse(from_to_split[3]), float.Parse(from_to_split[4]),
                                              float.Parse(from_to_split[5]));
            Vector3 center = (from_position + to_position) / 2.0f;
            float half_length = (to_position - from_position).magnitude / 2.0f;
            Quaternion rotation = Quaternion.LookRotation(to_position - from_position);
            return GeomController.CreateCapsule(
                parent, capsule_name, center, rotation, half_length, XmlUtils.GetFloat(geom_xml, "size", 1.0f));
        } else {
            Vector2 mujoco_size = XmlUtils.GetVector2WithDefaults(geom_xml, defaults, "size", Vector2.one);
            return GeomController.CreateCapsule(
                parent, capsule_name,
                XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "pos", Vector3.zero),
                XmlUtils.GetRotationWithDefaults(geom_xml, defaults, Quaternion.identity),
                mujoco_size.y,
                mujoco_size.x);
        }
    }

    private GeomController AddRobotMeshGeom(KineticHierarchyController parent, XmlNode geom_xml,
                                            XmlUtils.Defaults defaults, string mesh_name) {
        GeomController mesh_prefab = FindGeomPrefab(mesh_name);

        if (mesh_prefab == null) {
            Logger.Error("RobotLoader::AddRobotMeshGeom::Cannot find mesh prefab for: {0}", mesh_name);
            return null;
        }

        GeomController mesh_geom = Instantiate<GeomController>(mesh_prefab, parent.transform);
        mesh_geom.Initialize(parent, mesh_name,
                             XmlUtils.GetVector3WithDefaults(geom_xml, defaults, "pos", Vector3.zero),
                             XmlUtils.GetRotationWithDefaults(geom_xml, defaults, Quaternion.identity));
        return mesh_geom;
    }

    // Find the preloaded material and override with geom colors
    // (if defined).
    private void ResolveMaterial(XmlNode node_xml, XmlUtils.Defaults defaults, KineticHierarchyController mesh) {
        MeshRenderer[] renderers = mesh.GetComponentsInChildren<MeshRenderer>();

        string material_name = XmlUtils.GetStringWithDefaults(node_xml, defaults, "material", "");
        if (material_name != null) {

            if (materials_.ContainsKey(material_name)) {
                foreach (MeshRenderer mesh_renderer in renderers) {
                    mesh_renderer.material = materials_[material_name];
                }
            }
        }

        if (XmlUtils.HasAttributeWithDefaults(node_xml, defaults, "rgba")) {
            Color color = XmlUtils.GetColorWithDefaults(node_xml, defaults, "rgba", Color.white);
            foreach (MeshRenderer mesh_renderer in renderers) {
                if (mesh_renderer.material.HasProperty("_Color")) {
                    mesh_renderer.material.color = color;
                }
            }
        }
    }

    // When a geom references a mesh, it needs to be retreived
    // and cloned from the preloaded assets.
    private GeomController FindGeomPrefab(string prefab_name) {
        return FindGeomPrefabInPrefabSet(assembly_parts_.transform, prefab_name);
    }

    // Travel down preloaded assets hierarchy to find the prefab mesh.
    private GeomController FindGeomPrefabInPrefabSet(Transform prefab_set, string part_name) {
        string[] split_name = part_name.Split(new char[] { ':' }, 2);

        if (split_name.Length == 2) {
            Transform prefab_sub_set = SceneUtils.Find<Transform>(prefab_set, split_name[0], null);
            if (prefab_sub_set != null) {
                return FindGeomPrefabInPrefabSet(prefab_sub_set, split_name[1]);
            } else {
                Logger.Warning(
                    "RobotLoader::FindAssemblyPartPrefabInPrefabSet::Cannot find: {0} subset, trying default.",
                    split_name[0]);
                part_name = split_name[1];
            }
        }

        GeomController geom_prefab = SceneUtils.Find<GeomController>(prefab_set, part_name, null);
        if (geom_prefab != null) {
            return geom_prefab;
        }
        Logger.Warning("RobotLoader::FindAssemblyPartPrefabInPrefabSet::Cannot find assembly part: {0}", part_name);
        return null;
    }

    void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.3f, 0.0f, 0.3f));
    }
}
