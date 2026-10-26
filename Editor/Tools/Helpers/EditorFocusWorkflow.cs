// Copyright (C) KitWright. Licensed under MIT.
using System;
using System.Threading.Tasks;

namespace KitWright.Editor.Tools.Helpers
{
    // The workflow is separate from the OS/Unity adapter so refusals and delayed focus can be
    // tested without changing the desktop that is running the test suite.
    internal sealed class EditorFocusState
    {
        public int protocol = 1;
        public string platform;
        public int editorPid;
        public string projectPath;
        public bool supported;
        public bool batchMode;
        public bool busy;
        public bool modal;
        public int foregroundPid;
        public bool nativeWindowFocused;
        public bool applicationFocused;
        public string focusedWindowType;
        public int gameViewCount;
        public bool gameViewFocused;
        public string observationError;
        public double observedAt;

        public bool ForegroundVerified => supported && editorPid > 0 && foregroundPid == editorPid && nativeWindowFocused;
    }

    internal interface IEditorFocusBackend
    {
        double ElapsedMilliseconds { get; }
        EditorFocusState Capture();
        bool RequestApplicationFocus(out string reason);
        bool RequestGameViewFocus(out string reason);
        Task WaitForUpdate();
    }

    internal static class EditorFocusWorkflow
    {
        internal static async Task<object> Run(IEditorFocusBackend backend, bool gameView, int timeoutMs)
        {
            if (timeoutMs < 100 || timeoutMs > 5000)
                return Response.Error("INVALID_TIMEOUT", new { minimum = 100, maximum = 5000 });

            var before = backend.Capture();
            var after = before;
            bool activationRequested = false, gameViewRequested = false;
            bool? activationRequestAccepted = null, gameViewRequestAccepted = null;
            string activationReason = null, gameViewReason = null;
            var started = backend.ElapsedMilliseconds;
            object Failure(string code, string reason = null) => Response.Error(code, new
            {
                before, after, activationRequested, gameViewRequested,
                activationRequestAccepted, gameViewRequestAccepted, activationReason, gameViewReason, reason,
                inputVerified = false
            });

            string Guard(EditorFocusState state)
            {
                if (!state.supported) return "FOCUS_PLATFORM_UNSUPPORTED";
                if (state.batchMode) return "FOCUS_REQUIRES_INTERACTIVE_EDITOR";
                if (state.busy) return "EDITOR_BUSY";
                if (state.modal) return "EDITOR_MODAL_BLOCKED";
                if (!string.IsNullOrEmpty(state.observationError)) return "FOCUS_OBSERVATION_FAILED";
                if (state.editorPid != before.editorPid || state.projectPath != before.projectPath)
                    return "EDITOR_IDENTITY_CHANGED";
                if (gameView && state.gameViewCount == 0) return "GAME_VIEW_NOT_OPEN";
                if (gameView && state.gameViewCount > 1 && !state.gameViewFocused)
                    return "GAME_VIEW_AMBIGUOUS";
                return null;
            }

            var refused = Guard(before);
            if (refused != null) return Failure(refused);
            if (!before.ForegroundVerified)
            {
                activationRequested = true;
                activationRequestAccepted = backend.RequestApplicationFocus(out activationReason);
            }

            int stableObservations = 0;
            while (backend.ElapsedMilliseconds - started < timeoutMs)
            {
                // A request return value is not settled native state, whether true or false.
                // Observe later updates without retrying; success still needs two real samples.
                await backend.WaitForUpdate();
                after = backend.Capture();
                refused = Guard(after);
                if (refused != null) return Failure(refused);
                if (backend.ElapsedMilliseconds - started > timeoutMs) break;

                if (gameView && !gameViewRequested && after.ForegroundVerified)
                {
                    gameViewRequested = true;
                    gameViewRequestAccepted = backend.RequestGameViewFocus(out gameViewReason);
                    stableObservations = 0;
                    continue;
                }

                if (gameViewRequested && !after.ForegroundVerified)
                    return Failure("EDITOR_FOREGROUND_LOST");

                bool verified = after.ForegroundVerified && (!gameView || after.gameViewFocused);
                stableObservations = verified ? stableObservations + 1 : 0;
                if (stableObservations >= 2)
                    return Response.Success(gameView ? "Game View focus verified." : "Unity foreground verified.", new
                    {
                        before, after, activationRequested, gameViewRequested,
                        activationRequestAccepted, gameViewRequestAccepted, activationReason, gameViewReason,
                        foregroundVerified = true, gameViewFocusVerified = gameView && after.gameViewFocused,
                        inputVerified = false
                    }, "Focus is not proof that the game consumed input. Verify a causal input response separately.");
            }

            if (activationRequestAccepted == false && !after.ForegroundVerified)
                return Failure("FOREGROUND_REQUEST_REFUSED", activationReason);
            if (gameViewRequestAccepted == false && after.ForegroundVerified && !after.gameViewFocused)
                return Failure("GAME_VIEW_FOCUS_REFUSED", gameViewReason);
            return Failure(gameView && after.ForegroundVerified ? "GAME_VIEW_FOCUS_TIMEOUT" : "EDITOR_FOREGROUND_TIMEOUT");
        }
    }
}
