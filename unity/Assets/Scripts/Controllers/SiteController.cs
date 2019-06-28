using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Similar to geoms, sites also can contain primitive geometry.

public class SiteController : GeometricPrimitiveController {

    // Static counter used to generate names for anonymous sites.
    private static int site_count_ = 0;

    private static string ResolveName(string name) {
        if (name == null) {
            return string.Format("site_{0}", site_count_++);
        }
        return name;
    }

    public static SiteController CreateBox(KineticHierarchyController parent, string name, Vector3 position,
                                           Quaternion rotation, Vector3 box_size) {
        name = ResolveName(name);
        SiteController box_site = SceneUtils.InstantiateWithController<SiteController>(name);
        box_site.InitializeBox(parent, name, position, rotation, box_size);
        return box_site;
    }

    public static SiteController CreateSphere(KineticHierarchyController parent, string name, Vector3 position,
                                              Quaternion rotation, Vector3 sphere_size) {
        name = ResolveName(name);
        SiteController sphere_site = SceneUtils.InstantiateWithController<SiteController>(name);
        sphere_site.InitializeSphere(parent, name, position, rotation, sphere_size);
        return sphere_site;
    }

    public static SiteController CreatePlane(KineticHierarchyController parent, string name, Vector3 position,
                                             Quaternion rotation, Vector3 plane_size) {
        name = ResolveName(name);
        SiteController plane_site = SceneUtils.InstantiateWithController<SiteController>(name);
        plane_site.InitializePlane(parent, name, position, rotation, plane_size);
        return plane_site;
    }

    public static SiteController CreateCylinder(KineticHierarchyController parent, string name, Vector3 position,
                                                Quaternion rotation, Vector3 cylinder_size) {
        name = ResolveName(name);
        SiteController cylinder_site = SceneUtils.InstantiateWithController<SiteController>(name);
        cylinder_site.InitializeCylinder(parent, name, position, rotation, cylinder_size);
        return cylinder_site;
    }

    public static SiteController CreateCapsule(KineticHierarchyController parent, string name, Vector3 position,
                                               Quaternion rotation, float half_length, float radius) {
        name = ResolveName(name);
        SiteController capsule_geom = SceneUtils.InstantiateWithController<SiteController>(name);
        capsule_geom.InitializeCapsule(parent, name, position, rotation, half_length, radius);
        return capsule_geom;
    }
}
