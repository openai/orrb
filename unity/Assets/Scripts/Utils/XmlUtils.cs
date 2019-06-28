using System.Collections.Generic;
using UnityEngine;
using System.Xml;

// Utilities that support reading the MuJoCo XMLs and the property inheritance model that
// it uses.

public class XmlUtils {

    public class Defaults {
        public XmlNode root;
        public XmlNode current;

        public Defaults(XmlNode root) : this(root, root) { }

        public Defaults(XmlNode root, XmlNode current) {
            this.root = root;
            this.current = current;
        }

        public Defaults Resolve(string class_name) {
            if (class_name == null) {
                return this;
            }
            return Traverse(root, class_name);
        }

        private Defaults Traverse(XmlNode node, string class_name) {
            if (class_name.Equals(GetString(node, "class", null))) {
                return new Defaults(root, node);
            }

            foreach (XmlNode child_node in GetChildNodes(node, "default")) {
                Defaults child_defaults = Traverse(child_node, class_name);
                if (child_defaults != null) {
                    return child_defaults;
                }
            }

            return null;
        }

        public Defaults GetSubclass(string subclass_name) {
            if (current != null) {
                return new Defaults(root, GetChildNode(current, subclass_name));
            } else {
                return new Defaults(root, null);
            }
        }

    };

    public static XmlNode GetChildNode(XmlNode node, string name) {
        List<XmlNode> child_nodes = GetChildNodes(node, name);
        if (child_nodes.Count == 1) {
            return child_nodes[0];
        } else if (child_nodes.Count == 0) {
            return null;
        } else {
            Logger.Warning("RobotLoader::GetChildNode::Expected only 1 child: {0}, got {1}", name, child_nodes.Count);
            return null;
        }
    }

    public static List<XmlNode> GetChildNodes(XmlNode node, string name) {
        List<XmlNode> filtered_child_nodes = new List<XmlNode>();
        foreach (XmlNode child_node in node.ChildNodes) {
            if (child_node.Name.Equals(name)) {
                filtered_child_nodes.Add(child_node);
            }
        }
        return filtered_child_nodes;
    }

    public static bool HasAttributeWithDefaults(XmlNode node, Defaults defaults, string attribute_name) {
        if (defaults != null && defaults.current != null) {
            return HasAttribute(defaults.current, attribute_name) || HasAttribute(node, attribute_name);
        }
        return HasAttribute(node, attribute_name);
    }

    public static bool HasAttribute(XmlNode node, string attribute_name) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(attribute_name);
        return attribute_node != null;
    }

    public static Vector2 GetVector2WithDefaults(XmlNode node, Defaults defaults, string vector_name,
                                                 Vector2 default_value) {
        if (defaults != null && defaults.current != null) {
            return GetVector2(node, vector_name, GetVector2(defaults.current, vector_name, default_value));
        }
        return GetVector2(node, vector_name, default_value);
    }

    public static Vector2 GetVector2(XmlNode node, string name, Vector2 default_value) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(name);
        if (attribute_node != null) {
            return ParseVector2(attribute_node.Value, default_value);
        } else {
            return default_value;
        }
    }

    public static Vector3 GetVector3WithDefaults(XmlNode node, Defaults defaults, string vector_name,
                                                 Vector3 default_value) {
        if (defaults != null && defaults.current != null) {
            return GetVector3(node, vector_name, GetVector3(defaults.current, vector_name, default_value));
        }
        return GetVector3(node, vector_name, default_value);
    }

    public static Vector3 GetVector3(XmlNode node, string vector_name, Vector3 default_value) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(vector_name);
        if (attribute_node != null) {
            return ParseVector3(attribute_node.Value, default_value);
        } else {
            return default_value;
        }
    }

    public static Vector4 GetVector4WithDefaults(XmlNode node, Defaults defaults, string vector_name,
                                                 Vector4 default_value) {
        if (defaults != null && defaults.current != null) {
            return GetVector4(node, vector_name, GetVector4(defaults.current, vector_name, default_value));
        }
        return GetVector4(node, vector_name, default_value);
    }

    public static Vector4 GetVector4(XmlNode node, string name, Vector4 default_value) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(name);
        if (attribute_node != null) {
            return ParseVector4(attribute_node.Value, default_value);
        } else {
            return default_value;
        }
    }

    public static Quaternion GetRotationWithDefaults(XmlNode node, Defaults defaults, Quaternion default_value) {
        if (defaults != null && defaults.current != null) {
            return GetRotation(node, GetRotation(defaults.current, default_value));
        }
        return GetRotation(node, default_value);
    }

    public static Quaternion GetRotation(XmlNode node, Quaternion default_value) {
        if (HasAttribute(node, "euler")) {
            return Quaternion.Euler(XmlUtils.GetVector3(node, "euler", Vector3.zero) * Mathf.Rad2Deg);
        } else if (HasAttribute(node, "axisangle")) {
            Vector4 axis_angle = XmlUtils.GetVector4(node, "axisangle", Vector4.zero);
            return Quaternion.AngleAxis(axis_angle[3] * Mathf.Rad2Deg, new Vector3(axis_angle[0], axis_angle[1], axis_angle[2]));
        } else {
            return XmlUtils.GetQuaternion(node, "quat", default_value);
        }
    }

    public static Color GetColorWithDefaults(XmlNode node, Defaults defaults, string color_name, Color default_color) {
        if (defaults != null && defaults.current != null) {
            return GetColor(node, color_name, GetColor(defaults.current, color_name, default_color));
        }
        return GetColor(node, color_name, default_color);
    }

    public static Color GetColor(XmlNode node, string color_name, Color default_color) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(color_name);
        if (attribute_node != null) {
            return ParseColor(attribute_node.Value, default_color);
        } else {
            return default_color;
        }
    }

    public static Quaternion GetQuaternion(XmlNode node, string name, Quaternion default_value) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(name);
        if (attribute_node != null) {
            return ParseQuaternion(attribute_node.Value, default_value);
        } else {
            return default_value;
        }
    }

    public static string GetStringWithDefaults(XmlNode node, Defaults defaults, string string_name,
                                               string default_value) {
        if (defaults != null && defaults.current != null) {
            return GetString(node, string_name, GetString(defaults.current, string_name, default_value));
        }
        return GetString(node, string_name, default_value);
    }

    public static string GetString(XmlNode node, string string_name, string default_value) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(string_name);
        if (attribute_node != null) {
            return attribute_node.Value;
        } else {
            return default_value;
        }
    }

    public static float GetFloat(XmlNode node, string name, float default_value) {
        XmlNode attribute_node = node.Attributes.GetNamedItem(name);
        if (attribute_node != null) {
            return float.Parse(attribute_node.Value);
        } else {
            return default_value;
        }
    }

    public static Vector2 ParseVector2(string text, Vector2 default_vector) {
        string[] split = text.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 1) {
            return Vector2.one * float.Parse(split[0]);
        } else if (split.Length == 2 || split.Length == 3) {
            return new Vector2(float.Parse(split[0]), float.Parse(split[1]));
        } else {
            return default_vector;
        }
    }

    public static Vector3 ParseVector3(string text, Vector3 default_vector) {
        string[] split = text.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 1) {
            return Vector3.one * float.Parse(split[0]);
        } else if (split.Length == 3 || split.Length == 4) {
            return new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
        } else {
            return default_vector;
        }
    }

    public static Vector4 ParseVector4(string text, Vector4 default_vector) {
        string[] split = text.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (split.Length != 4) {
            return default_vector;
        } else {
            return new Vector4(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
        }
    }

    public static Color ParseColor(string text, Color default_color) {
        string[] split = text.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (split.Length != 4) {
            return default_color;
        } else {
            return new Color(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
        }
    }

    public static Quaternion ParseQuaternion(string text, Quaternion default_quaternion) {
        string[] split = text.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (split.Length != 4) {
            return default_quaternion;
        } else {
            return new Quaternion(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]), float.Parse(split[0]));
        }
    }
}
