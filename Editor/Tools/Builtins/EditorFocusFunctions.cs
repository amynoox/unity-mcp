// Copyright (C) KitWright. Licensed under MIT.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using KitWright.Editor.Tools.Helpers;

namespace KitWright.Editor.Tools.Builtins
{
    [ToolProvider("EditorFocus")]
    internal static class EditorFocusFunctions
    {
        [Description("Request OS foreground activation of this connected Unity Editor only, then verify its PID on two later editor updates. " +
                     "Windows/macOS only. Refuses batch mode, busy Editors, modal windows and OS denial. Does not unlock, send input, change permissions or prove game readiness.")]
        public static Task<object> FocusUnityEditor(
            [ToolParam("Verification timeout in milliseconds, 100-5000.", Required = false)] int timeout_ms = 2000)
            => Run(false, timeout_ms);

        [Description("Activate this connected Unity Editor and focus its existing Game View, then verify OS foreground and actual keyboard focus on two later updates. " +
                     "Requires an unambiguous open Game View; never creates a window or changes Play Mode. No simulated click/key or input-readiness claim.")]
        public static Task<object> FocusGameView(
            [ToolParam("Verification timeout in milliseconds, 100-5000.", Required = false)] int timeout_ms = 2000)
            => Run(true, timeout_ms);

        private static async Task<object> Run(bool gameView, int timeoutMs)
        {
            try { return await EditorFocusWorkflow.Run(new NativeEditorFocus(), gameView, timeoutMs); }
            catch (Exception ex)
            { return Response.Error("FOCUS_OPERATION_FAILED", new { reason = ex.GetType().Name, inputVerified = false }); }
        }
    }
}
