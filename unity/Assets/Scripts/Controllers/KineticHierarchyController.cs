using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Base class for scene elements that have the parent-child hierarchy,
// e.g.: bodies in bodies, geoms, sites, joints in bodies.

public class KineticHierarchyController : MonoBehaviour {

    public void Initialize(KineticHierarchyController parent, string name, Vector3 position, Quaternion rotation) {
        this.name = name;
        if (parent != null) {
            transform.parent = parent.transform;
        }
        transform.localPosition = position;
        transform.localRotation = rotation;
    }
}
