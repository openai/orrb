using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneUtils {

    // Create a named game object and attach a component to it.
    public static T InstantiateWithController<T>(string name) where T : Component {
        GameObject game_object = new GameObject();
        return game_object.AddComponent<T>() as T;
    }

    // Find a named child Component of a given type in a GameObject.
    public static T Find<T>(GameObject where, string name, T default_value) where T : Component {
        return Find<T>(where.transform, name, default_value);
    }

    // Find a named child Component of a given type in a Component.
    public static T Find<T>(Component where, string name, T default_value) where T : Component {
        Transform transform = where.transform;
        Transform child = transform.Find(name);

        if (child != null && child.GetComponent<T>() != null) {
            return child.GetComponent<T>();
        } else {
            return default_value;
        }
    }

    // Find a named child GameObject in a GameObject.
    public static GameObject Find(GameObject where, string name, GameObject default_value) {
        return Find(where.transform, name, default_value);
    }

    // Find a named child GameObject in a Component.
    public static GameObject Find(Component where, string name, GameObject default_value) {
        Transform transform = where.transform;
        Transform child = transform.Find(name);

        if (child != null) {
            return child.gameObject;
        } else {
            return default_value;
        }
    }
}
