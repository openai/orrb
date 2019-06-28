using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;

// This utility allows properties (class member variables) to be configured from commandline and
// property files. Use the Flag attribute to mark configurable variables. This utility depends
// on runtime reflexive introspection. 

[AttributeUsage(AttributeTargets.Field)]
public class Flag : Attribute {

}

public class Flags {

    static private Dictionary<string, object> object_flags_ = new Dictionary<string, object>();
    static private Dictionary<string, string> string_flags_ = new Dictionary<string, string>();

    static Flags() {
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args) {
            string[] split = arg.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (split.Length == 1) {
                string bool_flag = split[0];

                if (!bool_flag.StartsWith("--", StringComparison.Ordinal)) {
                    continue;
                }

                bool_flag = bool_flag.Substring(2);

                AddBoolFlag(bool_flag, true);
            } else if (split.Length == 2) {
                string key = split[0];
                string value = split[1];

                if (!key.StartsWith("--", StringComparison.Ordinal)) {
                    continue;
                }

                key = key.Substring(2);

                if ("flags.file".Equals(key)) {
                    LoadFlagsFromFile(value);
                } else {
                    string_flags_[key] = value;
                }
            }
        }
    }

    private static void LoadFlagsFromFile(string file_name) {
        Logger.Info("Flags::LoadFlagsFromFile::Reading flag file: {0}", file_name);
        StreamReader reader = new StreamReader(file_name);
        string line = "";
        while ((line = reader.ReadLine()) != null) {
            string[] split = line.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 2) {
                string flag_type = split[0];
                string key = split[1];
                if (typeof(bool).Name.Equals(flag_type)) {
                    AddBoolFlag(key, true);
                } else if (typeof(string).Name.Equals(flag_type)) {
                    string_flags_[key] = "";
                }
            } else if (split.Length == 3) {
                string flag_type = split[0];
                string key = split[1];
                string value = split[2];
                if (typeof(bool).Name.Equals(flag_type)) {
                    AddBoolFlag(key, bool.Parse(value));
                } else if (typeof(int).Name.Equals(flag_type)) {
                    object_flags_[key] = int.Parse(value);
                } else if (typeof(float).Name.Equals(flag_type)) {
                    object_flags_[key] = float.Parse(value);
                } else if (typeof(string).Name.Equals(flag_type)) {
                    object_flags_[key] = value;
                    string_flags_[key] = value;
                } else {
                    string_flags_[key] = value;
                }
            }
        }
        reader.Close();
    }

    static private void AddBoolFlag(string key, bool value) {
        if (key.StartsWith("no", System.StringComparison.Ordinal)) {
            object_flags_[key.Substring(2)] = !value;
        } else {
            object_flags_[key] = value;
        }
    }

    static public object Get<T>(string name, T default_value) {
        if (string_flags_.ContainsKey(name)) {
            string value = string_flags_[name];
            if (typeof(T).IsEnum) {
                T retval = (T)Enum.Parse(typeof(T), value, true);
                object_flags_[name] = retval;
                return retval;
            } else if (typeof(T).Equals(typeof(Vector3))) {
                Vector3 retval = ParseVector3(value);
                object_flags_[name] = retval;
                return retval;
            } else {
                T retval = (T)Convert.ChangeType(value, typeof(T));
                object_flags_[name] = retval;
                return retval;
            }
        } else if (object_flags_.ContainsKey(name)) {
            return (T)object_flags_[name];
        } else {
            object_flags_.Add(name, default_value);
            return default_value;
        }
    }

    static private Vector3 ParseVector3(string string_vec) {
        if (string_vec.StartsWith("(") && string_vec.EndsWith(")")) {
            string_vec = string_vec.Substring(1, string_vec.Length - 2);
        }
        string[] splits = string_vec.Split(new char[] { ',' });
        if (splits.Length != 3) {
            Logger.Error("Flags::ParseVector3::Invalid Vector3: {0}", string_vec);
            return Vector3.zero;
        }
        return new Vector3(float.Parse(splits[0].Trim()), float.Parse(splits[1].Trim()), float.Parse(splits[2].Trim()));
    }

    static public void DumpFlags() {
        SortedDictionary<string, string> flag_strings = new SortedDictionary<string, string>();
        foreach (KeyValuePair<string, object> pair in object_flags_) {
            if (pair.Value.GetType().IsEnum) {
                flag_strings.Add(pair.Key, string.Format("String {0} {1}", pair.Key, pair.Value));
            } else {
                flag_strings.Add(pair.Key, string.Format("{0} {1} {2}", pair.Value.GetType().Name, pair.Key, pair.Value));
            }
        }

        foreach (KeyValuePair<string, string> pair in flag_strings) {
            System.Console.WriteLine(pair.Value);
        }
    }

    static public void InitFlags(object subject, string prefix) {
        MethodInfo flag_getter = typeof(Flags).GetMethod("Get");
        foreach (FieldInfo field_info in subject.GetType().GetFields()) {
            if (field_info.IsDefined(typeof(Flag), true)) {
                MethodInfo typed_flag_getter = flag_getter.MakeGenericMethod(field_info.FieldType);
                field_info.SetValue(subject, typed_flag_getter.Invoke(null, new object[] {
                    GetFlagName (prefix, field_info.Name),
                    field_info.GetValue (subject)
                }));
            }
        }
    }

    static private string GetFlagName(string prefix, string flag_name) {
        if (flag_name.EndsWith("_")) {
            flag_name = flag_name.Substring(0, flag_name.Length - 1);
        }
        return string.Format("{0}.{1}", prefix, flag_name);
    }

}
