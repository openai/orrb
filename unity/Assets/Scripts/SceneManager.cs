using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The SceneManager knows how to create new instances of the prefabbed scene, and
// keeps track of those instances. OTOH right now we use just one scene per server.

public class SceneManager : MonoBehaviour {

    [SerializeField]
    public SceneInstance scene_instance_prefab_ = null;

    [SerializeField]
    public float scene_distance_ = 100.0f;

    private Dictionary<int, SceneInstance> scene_instances_ = new Dictionary<int, SceneInstance>();
    private int next_id_ = 0;

    public SceneInstance CreateSceneInstance() {
        SceneInstance scene_instance = Instantiate<SceneInstance>(scene_instance_prefab_);
        scene_instance.SetId(next_id_);
        scene_instances_.Add(next_id_, scene_instance);
        return scene_instance;
    }

    public SceneInstance GetSceneInstance(int id) {
        if (scene_instances_.ContainsKey(id)) {
            return scene_instances_[id];
        } else {
            return null;
        }
    }
}
