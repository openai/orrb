using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A base class for sites and geoms that can contain primitive shapes
// like: boxes, spheres, etc.

public class GeometricPrimitiveController : KineticHierarchyController {

    protected void InitializeBox(KineticHierarchyController parent, string box_name, Vector3 position,
                                   Quaternion rotation, Vector3 box_size) {
        Initialize(parent, box_name, position, rotation);
        GameObject the_box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(the_box.GetComponent<BoxCollider>());
        the_box.name = string.Format("{0}(primitive)", name);
        the_box.transform.parent = transform;
        the_box.transform.localPosition = Vector3.zero;
        the_box.transform.localRotation = Quaternion.identity;
        the_box.transform.localScale = box_size;
    }

    protected void InitializeSphere(KineticHierarchyController parent, string sphere_name, Vector3 position,
                                  Quaternion rotation, Vector3 sphere_size) {
        Initialize(parent, sphere_name, position, rotation);
        GameObject the_sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(the_sphere.GetComponent<SphereCollider>());
        the_sphere.name = string.Format("{0}(primitive)", name);
        the_sphere.transform.parent = transform;
        the_sphere.transform.localPosition = Vector3.zero;
        the_sphere.transform.localRotation = Quaternion.identity;
        the_sphere.transform.localScale = sphere_size;
    }

    protected void InitializePlane(KineticHierarchyController parent, string plane_name, Vector3 position,
                                 Quaternion rotation, Vector3 plane_size) {
        Initialize(parent, plane_name, position, rotation);
        GameObject the_plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Destroy(the_plane.GetComponent<MeshCollider>());
        the_plane.name = string.Format("{0}(primitive)", name);
        the_plane.transform.parent = transform;
        the_plane.transform.localPosition = Vector3.zero;
        the_plane.transform.localRotation = Quaternion.identity;
        the_plane.transform.localScale = plane_size;
    }

    protected void InitializeCylinder(KineticHierarchyController parent, string cylinder_name, Vector3 position,
                                    Quaternion rotation, Vector3 cylinder_size) {
        Initialize(parent, cylinder_name, position, rotation);
        GameObject the_cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(the_cylinder.GetComponent<MeshCollider>());
        the_cylinder.name = string.Format("{0}(primitive)", name);
        the_cylinder.transform.parent = transform;
        the_cylinder.transform.localPosition = Vector3.zero;
        the_cylinder.transform.localRotation = Quaternion.identity * Quaternion.Euler(90.0f, 0.0f, 0.0f);
        the_cylinder.transform.localScale = cylinder_size;
    }

    // A capsule is created from a cylinder and two spheres.
    protected void InitializeCapsule(KineticHierarchyController parent, string capsule_name, Vector3 position,
                                   Quaternion rotation, float half_length, float radius) {
        Initialize(parent, capsule_name, position, rotation);

        GameObject top_sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(top_sphere.GetComponent<SphereCollider>());
        top_sphere.name = string.Format("{0}(top_sphere)", name);
        top_sphere.transform.parent = transform;
        top_sphere.transform.localPosition = Vector3.forward * half_length;
        top_sphere.transform.localScale = Vector3.one * radius;
        top_sphere.transform.localRotation = Quaternion.identity;

        GameObject bottom_sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bottom_sphere.GetComponent<SphereCollider>());
        bottom_sphere.name = string.Format("{0}(bottom_sphere)", name);
        bottom_sphere.transform.parent = transform;
        bottom_sphere.transform.localPosition = Vector3.back * half_length;
        bottom_sphere.transform.localScale = Vector3.one * radius;
        bottom_sphere.transform.localRotation = Quaternion.identity;

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(cylinder.GetComponent<MeshCollider>());
        Destroy(cylinder.GetComponent<CapsuleCollider>());
        cylinder.name = string.Format("{0}(cylinder)", name);
        cylinder.transform.parent = transform;
        cylinder.transform.localPosition = Vector3.zero;
        cylinder.transform.localScale = new Vector3(radius, half_length, radius);
        cylinder.transform.localRotation = Quaternion.identity * Quaternion.Euler(90.0f, 0.0f, 0.0f);
    }
}
