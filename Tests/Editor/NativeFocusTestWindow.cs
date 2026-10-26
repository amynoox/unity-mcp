// Copyright (C) KitWright. Licensed under MIT.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    // Used only by the opt-in interactive test. It never targets another process,
    // sends input, grants permissions, or changes desktop/security settings.
    internal static class NativeFocusTestWindow
    {
        internal static bool BackgroundCurrentEditor()
        {
            if (Application.isBatchMode) return false;
#if UNITY_EDITOR_WIN
            var window = CurrentWindow();
            return window != IntPtr.Zero && ShowWindowAsync(window, 6); // SW_MINIMIZE
#elif UNITY_EDITOR_OSX
            var app = CurrentApplication();
            return app != IntPtr.Zero && SendBool(app, Selector("hide"));
#else
            return false;
#endif
        }

        internal static void RestoreCurrentEditorForCleanup()
        {
            if (Application.isBatchMode) return;
#if UNITY_EDITOR_WIN
            var window = CurrentWindow();
            if (window == IntPtr.Zero) return;
            ShowWindowAsync(window, 9); // SW_RESTORE
            SetForegroundWindow(window);
#elif UNITY_EDITOR_OSX
            var app = CurrentApplication();
            if (app == IntPtr.Zero) return;
            SendBool(app, Selector("unhide"));
            SendActivation(app, Selector("activateWithOptions:"), new UIntPtr(2));
#endif
        }

#if UNITY_EDITOR_WIN
        private static IntPtr CurrentWindow()
        {
            using (var process = Process.GetCurrentProcess())
            {
                var window = process.MainWindowHandle;
                GetWindowThreadProcessId(window, out var pid);
                return pid == process.Id ? window : IntPtr.Zero;
            }
        }
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);
#elif UNITY_EDITOR_OSX
        private static IntPtr CurrentApplication() => SendPointer(
            objc_getClass("NSRunningApplication"), Selector("currentApplication"));
        private static IntPtr Selector(string name) => sel_registerName(name);
        [DllImport("/usr/lib/libobjc.A.dylib")]
        private static extern IntPtr objc_getClass(string name);
        [DllImport("/usr/lib/libobjc.A.dylib")]
        private static extern IntPtr sel_registerName(string name);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendPointer(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendBool(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendActivation(IntPtr receiver, IntPtr selector, UIntPtr options);
#endif
    }
}
