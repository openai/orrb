using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A simple utility used to unify logging. The time logged is measured in milliseconds relative to
// application startup. An Id and indent / alignment value can be provided in order to make logs
// from multiple servers visually align nicer.

public class Logger {

    public enum Level {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        FATAL
    };

    private static string id_ = "";
    private static string id_aligned_ = "";

    public static void Initialize(string id, int alignment) {
        id_ = id;
        if (id_.Length > 0 && alignment > id_.Length) {
            id_aligned_ = id_.PadLeft(alignment);
        } else {
            id_aligned_ = id_;
        }
    }

    public static void Info(string format, params object[] objects) {
        Log(Level.INFO, format, objects);
    }

    public static void Warning(string format, params object[] objects) {
        Log(Level.WARNING, format, objects);
    }

    public static void Error(string format, params object[] objects) {
        Log(Level.ERROR, format, objects);
    }

    static void Log(Level level, string format, params object[] objects) {
        string message = string.Format(format, objects);
        if (id_aligned_.Length > 0) {
            message = string.Format("{0}::{1}::{2}::{3}", id_aligned_, LevelToString(level), Millis(), message);
        } else {
            message = string.Format("{0}::{1}::{2}", LevelToString(level), Millis(), message);
        }
        Consume(message);
    }

    private static void Consume(string message) {
        System.Console.WriteLine(message);
        if (Application.isEditor) {
            Debug.Log(message);
        }
    }

    private static string LevelToString(Level level) {
        switch (level) {
        case Level.DEBUG:
            return "D";
        case Level.INFO:
            return "I";
        case Level.WARNING:
            return "W";
        case Level.ERROR:
            return "E";
        default:
            return "X";
        }
    }

    private static string Millis() {
        return ((int)(Time.realtimeSinceStartup * 1000)).ToString("D10");
    }
}

