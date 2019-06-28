using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

// A class representing MuJoCo geom elements.

public class GeomController : GeometricPrimitiveController {

    public int category_id_ = 0;

    // Static counters used to generate names for anonymous primitive objects.
    private static int box_count_ = 0;
    private static int cylinder_count_ = 0;
    private static int plane_count_ = 0;
    private static int sphere_count_ = 0;
    private static int capsule_count_ = 0;

    private static string ResolveName(string prefix, ref int counter, string name) {
        if (name == null) {
            return string.Format("{0}_{1}", prefix, counter++);
        }
        return name;
    }

    // Helper factory methods used to create geometric primitives. Pass a
    // hierarchy parent, a name (or use a generated one, and basic parametric
    // geometry values.

    public static GeomController CreateBox(KineticHierarchyController parent, string name, Vector3 position,
                                           Quaternion rotation, Vector3 box_size) {
        name = ResolveName("box", ref box_count_, name);
        GeomController box_geom = SceneUtils.InstantiateWithController<GeomController>(name);
        box_geom.InitializeBox(parent, name, position, rotation, box_size);
        return box_geom;
    }

    public static GeomController CreateSphere(KineticHierarchyController parent, string name, Vector3 position,
                                              Quaternion rotation, Vector3 sphere_size) {
        name = ResolveName("sphere", ref sphere_count_, name);
        GeomController sphere_geom = SceneUtils.InstantiateWithController<GeomController>(name);
        sphere_geom.InitializeSphere(parent, name, position, rotation, sphere_size);
        return sphere_geom;
    }

    public static GeomController CreatePlane(KineticHierarchyController parent, string name, Vector3 position,
                                             Quaternion rotation, Vector3 plane_size) {
        name = ResolveName("plane", ref plane_count_, name);
        GeomController plane_geom = SceneUtils.InstantiateWithController<GeomController>(name);
        plane_geom.InitializePlane(parent, name, position, rotation, plane_size);
        return plane_geom;
    }

    public static GeomController CreateCylinder(KineticHierarchyController parent, string name, Vector3 position,
                                                Quaternion rotation, Vector3 cylinder_size) {
        if (name == null) {
            name = string.Format("cylinder_{0}", cylinder_count_++);
        }
        GeomController plane_geom = SceneUtils.InstantiateWithController<GeomController>(name);
        plane_geom.InitializeCylinder(parent, name, position, rotation, cylinder_size);
        return plane_geom;
    }

    public static GeomController CreateCapsule(KineticHierarchyController parent, string name, Vector3 position,
                                               Quaternion rotation, float half_length, float radius) {
        name = ResolveName("capsule", ref capsule_count_, name);
        GeomController capsule_geom = SceneUtils.InstantiateWithController<GeomController>(name);
        capsule_geom.InitializeCapsule(parent, name, position, rotation, half_length, radius);
        return capsule_geom;
    }

    public void SetCategoryRendererProperties() {
        // Create render PropertyBlocks with the appropriate category color
        // for each geom such that it can be rendered as a segmentation map.
        var mpb = new MaterialPropertyBlock();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) {
            var id = r.gameObject.GetInstanceID();
            var layer = r.gameObject.layer;

            mpb.SetColor("_CategoryColor", EncodeCategoryAsColor(category_id_));
            r.SetPropertyBlock(mpb);
        }
    }

    private static Color EncodeCategoryAsColor(int category_id) {
        // set all RGB channels to same value
        var color = new Color32(0, 0, 0, 255);
        color.r = (byte)(category_id);
        color.g = (byte)(category_id);
        color.b = (byte)(category_id);
        return color;
    }

    public static string GetGeomCategoryFromXml(XmlNode geom_xml) {
        string geom_name = XmlUtils.GetString(geom_xml, "name", null);
        if (geom_name == null) {
            return null;
        }
        string[] split_name = geom_name.Split(new char[] { ':' }, 2);
        if (split_name.Length > 1) {
            return split_name[0];
        }
        return null;
    }
}
