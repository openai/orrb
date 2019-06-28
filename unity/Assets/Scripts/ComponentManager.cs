using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// The ComponentManager is a instantiates, updates and runs the
// RendererComponents. It is attached to the scene instance. It
// can be configured with RendererComponentConfig protos. The
// RendererComponent is responsible for rendering the components    
// GUI in the interactive mode, and can produce a dump of the config.

public class ComponentManager : MonoBehaviour {

    // A IComponentBuilder knows how to build a RendererComponent
    // and attach it to a GameObject.
    private interface IComponentBuilder {
        RendererComponent Build(GameObject game_object);
    }

    // Template RendererComponent factory.
    private class ComponentBuilder<T> : IComponentBuilder where T : RendererComponent {
        public RendererComponent Build(GameObject game_object) {
            return game_object.AddComponent<T>();
        }
    }

    private static Dictionary<string, IComponentBuilder> builders_ = DefaultBuilders();

    // Here the supported RendererComponents are registered with
    // their names.
    private static Dictionary<string, IComponentBuilder> DefaultBuilders() {

        Assembly assembly = Assembly.GetExecutingAssembly();

        IEnumerable<Type> renderer_component_types =
            assembly.GetTypes().Where(typeof(RendererComponent).IsAssignableFrom)
                    .Where(t => typeof(RendererComponent) != t);

        Dictionary<string, IComponentBuilder> builders = new Dictionary<string, IComponentBuilder>();
        Type builder_type = typeof(ComponentBuilder<>);

        foreach (Type renderer_component_type in renderer_component_types) {
            builders.Add(
                renderer_component_type.Name, Activator.CreateInstance(
                    builder_type.MakeGenericType(renderer_component_type))
                as IComponentBuilder);
        }

        return builders;
    }

    // A live component.
    [System.Serializable]
    public class ComponentInstance {

        [SerializeField]
        public RendererComponent renderer_component;

        [SerializeField]
        public string type;

        [SerializeField]
        public string name;

        [SerializeField]
        public string path;

        [SerializeField]
        public bool enabled;

        [SerializeField]
        public bool hidden;

        public ComponentInstance(RendererComponent renderer_component, string type, string name, string path,
                                 bool enabled) {
            this.renderer_component = renderer_component;
            this.type = type;
            this.name = name;
            this.path = path;
            this.enabled = enabled;
            this.hidden = true;
        }
    }

    [SerializeField]
    public List<ComponentInstance> components_ = new List<ComponentInstance>();

    private Dictionary<string, ComponentInstance> components_dictionary_ = new Dictionary<string, ComponentInstance>();

    private static char[] path_separator_ = new char[] { '/' };

    private Vector2 scroll_position_ = Vector2.zero;

    // Traverse the kinetic hierarchy in order to find the object.
    private static GameObject FindSubject(GameObject parent, string path) {

        if (path == null || path.Length == 0) {
            return parent;
        }
        string[] split = path.Split(path_separator_, 2, System.StringSplitOptions.RemoveEmptyEntries);

        Transform child = parent.transform.Find(split[0]);
        if (child == null) {
            Logger.Error("ComponentManager::FindSubject::Cannot find: {0}", split[0]);
            return null;
        }

        if (split.Length == 1) {
            return child.gameObject;
        } else if (split.Length == 2) {
            return FindSubject(child.gameObject, split[1]);
        }

        Logger.Error("");
        return null;
    }

    // Instantiate a component of a given type and name in an object
    // that is located based on the component_path.
    private ComponentInstance InstantiateComponentByType(string type, string component_name, string component_path,
                                                         bool component_enabled) {
        GameObject subject = FindSubject(this.gameObject, component_path);
        if (builders_.ContainsKey(type)) {
            RendererComponent renderer_component = builders_[type].Build(subject);
            if (renderer_component == null) {
                Logger.Error("ComponentManager::InstantiateComponentByType::Builder returned null for: {0} type: {1}.",
                             component_name, type);
                return null;
            }
            return new ComponentInstance(renderer_component, type, component_name, component_path, component_enabled);
        }

        Logger.Error("ComponentManager::InstantiateComponentByType::Unknown component type: {0}.", type);
        return null;
    }

    // Instantiate a component of a given type, name and config in the scene.
    public bool AddComponent(string type, string name, string path, Orrb.RendererComponentConfig config, bool enabled) {
        if (components_dictionary_.ContainsKey(name)) {
            Logger.Error("ComponentManager::AddComponent::Component already exists: {0}.", name);
            return false;
        }

        ComponentInstance component_instance = InstantiateComponentByType(type, name, path, enabled);

        if (component_instance == null) {
            Logger.Error("ComponentManager::AddComponent::Failed to instantiate component: {0} ({1}).", name, type);
            return false;
        }

        if (!component_instance.renderer_component.InitializeComponent(config)) {
            Logger.Error("ComponentManager::AddComponent::Failed to initialize: {0} of type: {1}.", name, type);
            return false;
        }

        components_.Add(component_instance);
        components_dictionary_.Add(name, component_instance);

        return true;
    }

    public bool RemoveComponent(string name) {
        return false;
    }

    public bool UpdateComponent(string name, Orrb.RendererComponentConfig config) {
        if (components_dictionary_.ContainsKey(name)) {
            return components_dictionary_[name].renderer_component.UpdateComponent(config);
        } else {
            Logger.Warning("ComponentManager::UpdateComponent::Cannot find component: {0}.", name);
            return false;
        }
    }

    public bool RunComponents(RendererComponent.IOutputContext context) {
        foreach (ComponentInstance component_instance in components_) {
            if (component_instance.enabled) {
                if (!component_instance.renderer_component.RunComponent(context)) {
                    Logger.Warning("ComponentManager::RunComponents::Failed running: {0}.", component_instance.name);
                }
            }
        }
        return true;
    }

    public void DrawEditorGUI() {
        scroll_position_ = GUILayout.BeginScrollView(scroll_position_, GUILayout.ExpandWidth(true));
        GUILayout.BeginVertical();
        foreach (ComponentInstance component_instance in components_) {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(component_instance.hidden ? "+" : "-", GUILayout.Width(20))) {
                component_instance.hidden = !component_instance.hidden;
            }
            if (GUILayout.Button("Run", GUILayout.Width(40))) {
                component_instance.renderer_component.RunComponent(new RendererComponent.NullOutputContext());
            }

            component_instance.enabled = GUILayout.Toggle(component_instance.enabled,
                                                          string.Format(" {0}", component_instance.name),
                                                          GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            if (!component_instance.hidden) {
                GUILayout.BeginHorizontal();
                component_instance.renderer_component.DrawEditorGUI();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(3);
        }
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
    }

    public void DrawSceneGUI() {
        foreach (ComponentInstance component_instance in components_) {
            if (component_instance.enabled) {
                component_instance.renderer_component.DrawSceneGUI();
            }
        }
    }

    // Produce a RendererConfig based on current state of all the RendererComponents.
    public Orrb.RendererConfig GetConfig() {
        Orrb.RendererConfig config = new Orrb.RendererConfig();
        foreach (ComponentInstance component_instance in components_) {
            Orrb.RendererComponent component_item = new Orrb.RendererComponent();
            component_item.Name = component_instance.name;
            component_item.Type = component_instance.type;
            component_item.Path = component_instance.path;
            component_item.Config = component_instance.renderer_component.GetConfig();
            config.Components.Add(component_item);
        }
        return config;
    }
}
