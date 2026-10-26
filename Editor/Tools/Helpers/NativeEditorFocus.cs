// Copyright (C) KitWright. Licensed under MIT.
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using KitWright.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace KitWright.Editor.Tools.Helpers
{
    internal sealed class NativeEditorFocus : IEditorFocusBackend
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public double ElapsedMilliseconds => clock.Elapsed.TotalMilliseconds;
        public Task WaitForUpdate() => Task.Delay(50); // Retains the Unity synchronization context.

        private static EditorWindow[] GameViews() => Resources.FindObjectsOfTypeAll<EditorWindow>()
            .Where(w => w != null && w.GetType().FullName == "UnityEditor.GameView").ToArray();

        public EditorFocusState Capture()
        {
            var windows = GameViews();
            var focused = EditorWindow.focusedWindow;
            var state = new EditorFocusState
            {
                platform = Application.platform.ToString(),
                editorPid = CurrentPid(),
                projectPath = ApplicationPaths.ProjectRoot.Replace('\\', '/'),
                batchMode = Application.isBatchMode,
                busy = EditorApplication.isCompiling || EditorApplication.isUpdating ||
                       (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying),
                applicationFocused = Application.isFocused,
                focusedWindowType = focused != null ? focused.GetType().FullName : null,
                gameViewCount = windows.Length,
                gameViewFocused = focused != null && focused.hasFocus && windows.Contains(focused),
                observedAt = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds
            };
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
            state.supported = true;
            if (!state.batchMode)
            {
                try
                {
                    state.foregroundPid = ForegroundPid();
                    state.nativeWindowFocused = NativeWindowFocused(state.foregroundPid, state.editorPid);
                    state.modal = HasModalWindow();
#if UNITY_EDITOR_OSX
                    var app = SendPointer(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
                    state.nativeMainThread = SendBool(objc_getClass("NSThread"), sel_registerName("isMainThread"));
                    state.nativeApplicationHidden = SendBool(app, sel_registerName("isHidden"));
                    state.nativeActivationPolicy = SendInt(app, sel_registerName("activationPolicy"));
#endif
                }
                catch (Exception ex) { state.observationError = ex.GetType().Name; }
            }
#endif
            return state;
        }

        public bool RequestApplicationFocus(out string reason)
        {
            reason = null;
            try
            {
#if UNITY_EDITOR_WIN
                var window = MainWindow();
                GetWindowThreadProcessId(window, out var windowPid);
                if (window == IntPtr.Zero || windowPid != CurrentPid() || !IsWindowEnabled(window))
                { reason = "No enabled main window belonging to this Editor."; return false; }
                if (IsIconic(window)) ShowWindowAsync(window, 9); // SW_RESTORE, only this Editor.
                if (SetForegroundWindow(window)) return true;
                reason = "Windows refused this Editor's foreground request.";
#elif UNITY_EDITOR_OSX
                if (!SendBool(objc_getClass("NSThread"), sel_registerName("isMainThread")))
                { reason = "AppKit focus requires the Editor main thread."; return false; }
                var app = SendPointer(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
                if (app == IntPtr.Zero) { reason = "This Editor has no native application."; return false; }
                // Restore only our hidden Editor. Use AppKit's normal self-activation,
                // never an arbitrary process or an ignored/forced activation option.
                if (SendBool(app, sel_registerName("isHidden")))
                    SendVoidArgument(app, sel_registerName("unhide:"), IntPtr.Zero);
                var activate = sel_registerName("activate");
                if (SendBoolArgument(app, sel_registerName("respondsToSelector:"), activate))
                    SendVoid(app, activate); // macOS 14+ cooperative activation.
                else
                    SendVoidBool(app, sel_registerName("activateIgnoringOtherApps:"), false);
                // A hidden app may have neither window until AppKit processes the
                // unhide request. Do not refuse before unhide, or require a synchronous
                // restoration: later observations still have to verify a key window.
                var main = SendPointer(app, sel_registerName("mainWindow"));
                if (main == IntPtr.Zero) main = SendPointer(app, sel_registerName("keyWindow"));
                // Making a window key does not itself establish application activation.
                // The workflow must still verify both facts on later Editor updates.
                if (main != IntPtr.Zero)
                    SendVoidArgument(main, sel_registerName("makeKeyAndOrderFront:"), IntPtr.Zero);
                return true; // Request issued, not a claim that focus was granted.
#else
                reason = "Only Windows and macOS interactive Editors are supported.";
#endif
            }
            catch (Exception ex) { reason = ex.GetType().Name; }
            return false;
        }

        public bool RequestGameViewFocus(out string reason)
        {
            var windows = GameViews();
            var target = windows.FirstOrDefault(w => w == EditorWindow.focusedWindow) ??
                         (windows.Length == 1 ? windows[0] : null);
            if (target == null)
            { reason = "An unambiguous existing Game View is required; no window was created."; return false; }
            target.Show();
            target.Focus();
            target.Repaint();
            reason = null;
            return true;
        }

        private static int CurrentPid()
        {
            using (var process = Process.GetCurrentProcess()) return process.Id;
        }

#if UNITY_EDITOR_WIN
        private static IntPtr MainWindow()
        {
            using (var process = Process.GetCurrentProcess()) return process.MainWindowHandle;
        }
        private static int ForegroundPid()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            return (int)pid;
        }
        private static bool NativeWindowFocused(int foreground, int editor) => foreground == editor;
        private static bool HasModalWindow()
        {
            var window = MainWindow();
            return window != IntPtr.Zero && !IsWindowEnabled(window);
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindowAsync(IntPtr window, int command);
#elif UNITY_EDITOR_OSX
        private static int ForegroundPid()
        {
            var workspace = SendPointer(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
            var front = SendPointer(workspace, sel_registerName("frontmostApplication"));
            return front == IntPtr.Zero ? 0 : SendInt(front, sel_registerName("processIdentifier"));
        }
        private static bool NativeWindowFocused(int foreground, int editor)
        {
            if (foreground != editor) return false;
            var app = SendPointer(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            var key = SendPointer(app, sel_registerName("keyWindow"));
            return key != IntPtr.Zero && SendBool(key, sel_registerName("isKeyWindow"));
        }
        private static bool HasModalWindow()
        {
            var app = SendPointer(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            if (SendPointer(app, sel_registerName("modalWindow")) != IntPtr.Zero) return true;
            var main = SendPointer(app, sel_registerName("mainWindow"));
            return main != IntPtr.Zero && SendPointer(main, sel_registerName("attachedSheet")) != IntPtr.Zero;
        }
        [DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr objc_getClass(string name);
        [DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr sel_registerName(string name);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendPointer(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern int SendInt(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendBool(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendBoolArgument(IntPtr receiver, IntPtr selector, IntPtr argument);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoid(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoidBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool argument);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoidArgument(IntPtr receiver, IntPtr selector, IntPtr argument);
#endif
    }
}
