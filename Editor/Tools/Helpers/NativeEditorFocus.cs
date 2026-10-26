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
                var runningClass = objc_getClass("NSRunningApplication");
                var current = SendPointer(runningClass, sel_registerName("currentApplication"));
                var bundle = SendPointer(current, sel_registerName("bundleIdentifier"));
                var url = SendPointer(current, sel_registerName("bundleURL"));
                if (bundle == IntPtr.Zero || url == IntPtr.Zero)
                { reason = "This Editor has no native bundle identity."; return false; }
                var instances = SendPointerArgument(runningClass,
                    sel_registerName("runningApplicationsWithBundleIdentifier:"), bundle);
                if (instances == IntPtr.Zero || SendUnsigned(instances, sel_registerName("count")).ToUInt64() != 1 ||
                    SendInt(SendPointerIndex(instances, sel_registerName("objectAtIndex:"), UIntPtr.Zero),
                        sel_registerName("processIdentifier")) != CurrentPid())
                { reason = "Existing Editor activation is ambiguous; no workspace request was sent."; return false; }
                var workspace = SendPointer(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
                var open = sel_registerName("openApplicationAtURL:configuration:completionHandler:");
                var configurationClass = objc_getClass("NSWorkspaceOpenConfiguration");
                if (configurationClass == IntPtr.Zero || workspace == IntPtr.Zero ||
                    !SendBoolArgument(workspace, sel_registerName("respondsToSelector:"), open))
                { reason = "Existing-instance workspace activation is unavailable."; return false; }
                var configuration = SendPointer(configurationClass, sel_registerName("configuration"));
                if (configuration == IntPtr.Zero)
                { reason = "Existing-instance workspace configuration is unavailable."; return false; }
                foreach (var setter in new[] { "setCreatesNewApplicationInstance:",
                    "setAllowsRunningApplicationSubstitution:", "setPromptsUserIfNeeded:",
                    "setAddsToRecentItems:", "setHides:", "setHidesOthers:", "setActivates:" })
                    if (!SendBoolArgument(configuration, sel_registerName("respondsToSelector:"), sel_registerName(setter)))
                    { reason = "A required existing-instance workspace guard is unavailable."; return false; }
                // NSWorkspace supplies cooperative activation context. Select the sole
                // already-running Editor at its own bundle URL, never a new instance,
                // another installation, a document, arguments, or an authentication UI.
                SendVoidBool(configuration, sel_registerName("setCreatesNewApplicationInstance:"), false);
                SendVoidBool(configuration, sel_registerName("setAllowsRunningApplicationSubstitution:"), false);
                SendVoidBool(configuration, sel_registerName("setPromptsUserIfNeeded:"), false);
                SendVoidBool(configuration, sel_registerName("setAddsToRecentItems:"), false);
                SendVoidBool(configuration, sel_registerName("setHides:"), false);
                SendVoidBool(configuration, sel_registerName("setHidesOthers:"), false);
                SendVoidBool(configuration, sel_registerName("setActivates:"), true);
                if (SendBool(app, sel_registerName("isHidden")))
                    SendVoid(app, sel_registerName("unhideWithoutActivation"));
                SendVoidThreeArguments(workspace, open, url, configuration, IntPtr.Zero);
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
        private static extern IntPtr SendPointerArgument(IntPtr receiver, IntPtr selector, IntPtr argument);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern UIntPtr SendUnsigned(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendPointerIndex(IntPtr receiver, IntPtr selector, UIntPtr index);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoidThreeArguments(IntPtr receiver, IntPtr selector, IntPtr first, IntPtr second, IntPtr third);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoid(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoidBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool argument);
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendVoidArgument(IntPtr receiver, IntPtr selector, IntPtr argument);
#endif
    }
}
