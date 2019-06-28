using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// ConfigUtilities provide a reflection based interoperation between the C# components and protocol
// buffer based RendererComponentConfig structures. The helper functions: Get/SetProperty allow easy
// extraction / injection of singular configurable values. The Get/SetProperties methods allow
// configuring of whole components, and depend on runtime, reflective introspection of the component.
// The ConfigProperty attribute is used to mark configurable fields.

[AttributeUsage(AttributeTargets.Field)]
public class ConfigProperty : Attribute {

}

public class ConfigUtils {

    public static float GetProperty(string name, Orrb.RendererComponentConfig config, float default_value) {
        if (config != null && config.FloatProperties.ContainsKey(name)) {
            return config.FloatProperties[name];
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, float value) {
        config.FloatProperties.Add(name, value);
    }

    public static int GetProperty(string name, Orrb.RendererComponentConfig config, int default_value) {
        if (config != null && config.IntProperties.ContainsKey(name)) {
            return config.IntProperties[name];
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, int value) {
        config.IntProperties.Add(name, value);
    }

    public static string GetProperty(string name, Orrb.RendererComponentConfig config, string default_value) {
        if (config != null && config.StringProperties.ContainsKey(name)) {
            return config.StringProperties[name];
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, string value) {
        config.StringProperties.Add(name, value);
    }

    public static bool GetProperty(string name, Orrb.RendererComponentConfig config, bool default_value) {
        if (config != null && config.BoolProperties.ContainsKey(name)) {
            return config.BoolProperties[name];
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, bool value) {
        config.BoolProperties.Add(name, value);
    }

    public static Vector3 GetProperty(string name, Orrb.RendererComponentConfig config, Vector3 default_value) {
        if (config != null && config.Vector3Properties.ContainsKey(name)) {
            Orrb.Vector3 vector3 = config.Vector3Properties[name];
            return new Vector3(vector3.X, vector3.Y, vector3.Z);
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, Vector3 value) {
        Orrb.Vector3 vector3 = new Orrb.Vector3();
        vector3.X = value.x;
        vector3.Y = value.y;
        vector3.Z = value.z;
        config.Vector3Properties.Add(name, vector3);
    }

    public static Vector2 GetProperty(string name, Orrb.RendererComponentConfig config, Vector2 default_value) {
        if (config != null && config.Vector2Properties.ContainsKey(name)) {
            Orrb.Vector2 vector2 = config.Vector2Properties[name];
            return new Vector2(vector2.X, vector2.Y);
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, Vector2 value) {
        Orrb.Vector2 vector2 = new Orrb.Vector2();
        vector2.X = value.x;
        vector2.Y = value.y;
        config.Vector2Properties.Add(name, vector2);
    }

    public static Quaternion GetProperty(string name, Orrb.RendererComponentConfig config, Quaternion default_value) {
        if (config != null && config.QuaternionProperties.ContainsKey(name)) {
            Orrb.Quaternion quaternion = config.QuaternionProperties[name];
            return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, Quaternion value) {
        Orrb.Quaternion quaternion = new Orrb.Quaternion();
        quaternion.X = value.x;
        quaternion.Y = value.y;
        quaternion.Z = value.z;
        quaternion.W = value.w;
        config.QuaternionProperties.Add(name, quaternion);
    }

    public static Color GetProperty(string name, Orrb.RendererComponentConfig config, Color default_value) {
        if (config != null && config.ColorProperties.ContainsKey(name)) {
            Orrb.Color color = config.ColorProperties[name];
            return new Color(color.R, color.G, color.B, color.A);
        }
        return default_value;
    }

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, Color value) {
        Orrb.Color color = new Orrb.Color();
        color.R = value.r;
        color.G = value.g;
        color.B = value.b;
        color.A = value.a;
        config.ColorProperties.Add(name, color);
    }

    public static T GetEnumProperty<T>(string name, Orrb.RendererComponentConfig config, T default_value)
        where T : struct, IConvertible {
        if (config != null && config.EnumProperties.ContainsKey(name)) {
            return (T)Enum.Parse(typeof(T), config.EnumProperties[name]);
        }
        return default_value;
    }

    public static void SetEnumProperty<T>(string name, Orrb.RendererComponentConfig config, T value)
        where T : struct, IConvertible {
        config.EnumProperties.Add(name, value.ToString());
    }

    public static void GetProperties(object subject, Orrb.RendererComponentConfig config) {
        Type utils = typeof(ConfigUtils);
        Type[] getter_types = new Type[] { typeof(string), typeof(Orrb.RendererComponentConfig), typeof(string) };
        object[] getter_parameters = { null, config, null };
        MethodInfo enum_property_getter = utils.GetMethod("GetEnumProperty");

        foreach (FieldInfo field_info in subject.GetType().GetFields()) {
            if (field_info.IsDefined(typeof(ConfigProperty), true)) {
                MethodInfo property_getter = null;
                if (field_info.FieldType.IsEnum) {
                    property_getter = enum_property_getter.MakeGenericMethod(field_info.FieldType);
                } else {
                    getter_types[2] = field_info.FieldType;
                    property_getter = utils.GetMethod("GetProperty", getter_types);
                }

                if (property_getter == null) {
                    Logger.Error("Cannot get property getter for: {0}", field_info.FieldType);
                } else {
                    getter_parameters[0] = GetPropertyName(field_info.Name);
                    getter_parameters[2] = field_info.GetValue(subject);
                    field_info.SetValue(subject, property_getter.Invoke(null, getter_parameters));
                }
            }
        }
    }

    private static string GetPropertyName(string name) {
        if (name.EndsWith("_", StringComparison.Ordinal)) {
            return name.Substring(0, name.Length - 1);
        }
        return name;
    }

    public static void SetProperties(object subject, Orrb.RendererComponentConfig config) {
        Type utils = typeof(ConfigUtils);
        Type[] setter_types = new Type[] { typeof(string), typeof(Orrb.RendererComponentConfig), typeof(string) };
        object[] setter_parameters = { null, config, null };
        MethodInfo enum_property_setter = utils.GetMethod("SetEnumProperty");

        foreach (FieldInfo field_info in subject.GetType().GetFields()) {
            if (field_info.IsDefined(typeof(ConfigProperty), true)) {
                MethodInfo property_setter = null;
                if (field_info.FieldType.IsEnum) {
                    property_setter = enum_property_setter.MakeGenericMethod(field_info.FieldType);
                } else {
                    setter_types[2] = field_info.FieldType;
                    property_setter = utils.GetMethod("SetProperty", setter_types);
                }

                if (property_setter == null) {
                    Logger.Error("Cannot get property setter for: {0}.", field_info.FieldType);
                } else {
                    setter_parameters[0] = GetPropertyName(field_info.Name);
                    setter_parameters[2] = field_info.GetValue(subject);
                    property_setter.Invoke(null, setter_parameters);
                }
            }
        }
    }

    public static string ResolveFile(string basedir, string file_path) {
	    if (Path.IsPathRooted(file_path)) {
		    return file_path;
	    } else {
	    	return Path.Combine(basedir, file_path);
        }
    }

}

