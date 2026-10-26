// Copyright (C) KitWright. Licensed under MIT.
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using KitWright.Editor.MCP.Server;
using KitWright.Editor.Tools;
using KitWright.Editor.Tools.Builtins;
using KitWright.Editor.Tools.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace KitWright.Editor.Tests
{
    public sealed class EditorFocusFunctionsTests
    {
        private sealed class Backend : IEditorFocusBackend
        {
            internal EditorFocusState State = new EditorFocusState
            {
                supported = true, editorPid = 42, foregroundPid = 7,
                projectPath = "/test", gameViewCount = 1, nativeWindowFocused = true
            };
            internal bool ActivateAllowed = true, FocusAllowed = true;
            internal Action<int, Backend> Tick;
            internal int Activations, Focuses, Updates;
            public double ElapsedMilliseconds { get; private set; }
            public EditorFocusState Capture() => new EditorFocusState
            {
                supported = State.supported, batchMode = State.batchMode, busy = State.busy,
                modal = State.modal, editorPid = State.editorPid, foregroundPid = State.foregroundPid,
                nativeWindowFocused = State.nativeWindowFocused,
                projectPath = State.projectPath, gameViewCount = State.gameViewCount,
                applicationFocused = State.applicationFocused,
                gameViewFocused = State.gameViewFocused, observationError = State.observationError
            };
            public bool RequestApplicationFocus(out string reason)
            { Activations++; reason = "OS refusal"; return ActivateAllowed; }
            public bool RequestGameViewFocus(out string reason)
            { Focuses++; reason = "Window refusal"; return FocusAllowed; }
            public Task WaitForUpdate()
            {
                ElapsedMilliseconds += 50;
                Updates++;
                Tick?.Invoke(Updates, this);
                return Task.CompletedTask;
            }
        }

        private static JObject Json(object result) => JObject.FromObject(result);
        private static JObject Run(Backend backend, bool game = false, int timeout = 500)
            => Json(EditorFocusWorkflow.Run(backend, game, timeout).GetAwaiter().GetResult());

        [TestCase(99)] [TestCase(5001)]
        public void InvalidTimeoutNeverRequestsActivation(int timeout)
        {
            var backend = new Backend();
            Assert.AreEqual("INVALID_TIMEOUT", (string)Run(backend, timeout: timeout)["code"]);
            Assert.Zero(backend.Activations);
        }

        [TestCase("batchMode", "FOCUS_REQUIRES_INTERACTIVE_EDITOR")]
        [TestCase("busy", "EDITOR_BUSY")]
        [TestCase("modal", "EDITOR_MODAL_BLOCKED")]
        [TestCase("unsupported", "FOCUS_PLATFORM_UNSUPPORTED")]
        public void UnsafeInitialStateDoesNotTouchDesktop(string field, string code)
        {
            var backend = new Backend();
            if (field == "batchMode") backend.State.batchMode = true;
            if (field == "busy") backend.State.busy = true;
            if (field == "modal") backend.State.modal = true;
            if (field == "unsupported") backend.State.supported = false;
            Assert.AreEqual(code, (string)Run(backend)["code"]);
            Assert.Zero(backend.Activations);
            Assert.Zero(backend.Focuses);
        }

        [Test]
        public void FailedObservationIsNotForegroundProof()
        {
            var backend = new Backend();
            backend.State.observationError = "Native error";
            Assert.AreEqual("FOCUS_OBSERVATION_FAILED", (string)Run(backend)["code"]);
            Assert.Zero(backend.Activations);
        }

        [Test]
        public void RefusedActivationIsNotRetried()
        {
            var backend = new Backend { ActivateAllowed = false };
            Assert.AreEqual("FOREGROUND_REQUEST_REFUSED", (string)Run(backend)["code"]);
            Assert.AreEqual(1, backend.Activations);
            Assert.Zero(backend.Focuses);
        }

        [Test]
        public void AcceptedRequestWithoutForegroundTimesOutRatherThanPassing()
        {
            var backend = new Backend();
            var result = Run(backend);
            Assert.IsFalse((bool)result["success"]);
            Assert.AreEqual("EDITOR_FOREGROUND_TIMEOUT", (string)result["code"]);
            Assert.AreEqual(1, backend.Activations);
            Assert.LessOrEqual(backend.ElapsedMilliseconds, 500);
        }

        [Test]
        public void FocusOnlyOnLastObservationDoesNotPassStabilityGate()
        {
            var backend = new Backend { Tick = (n, b) => { if (n == 10) b.State.foregroundPid = 42; } };
            Assert.AreEqual("EDITOR_FOREGROUND_TIMEOUT", (string)Run(backend)["code"]);
            Assert.AreEqual(10, backend.Updates);
        }

        [Test]
        public void DelayedForegroundRequiresTwoObservationsAndDoesNotAttestInput()
        {
            var backend = new Backend { Tick = (n, b) => { if (n >= 3) b.State.foregroundPid = 42; } };
            var result = Run(backend);
            Assert.IsTrue((bool)result["success"]);
            Assert.AreEqual(4, backend.Updates);
            Assert.AreEqual(1, backend.Activations);
            Assert.IsFalse((bool)result["data"]["inputVerified"]);
        }

        [Test]
        public void UnityApplicationFlagAloneIsNotOSForegroundProof()
        {
            var backend = new Backend();
            backend.State.applicationFocused = true;
            Assert.IsFalse((bool)Run(backend)["success"]);
        }

        [Test]
        public void AlreadyForegroundDoesNotRequestActivation()
        {
            var backend = new Backend();
            backend.State.foregroundPid = 42;
            Assert.IsTrue((bool)Run(backend)["success"]);
            Assert.Zero(backend.Activations);
            Assert.AreEqual(2, backend.Updates);
        }

        [Test]
        public void ForegroundApplicationWithoutKeyWindowStillNeedsActivation()
        {
            var backend = new Backend { Tick = (n, b) => b.State.nativeWindowFocused = true };
            backend.State.foregroundPid = 42;
            backend.State.nativeWindowFocused = false;
            Assert.IsTrue((bool)Run(backend)["success"]);
            Assert.AreEqual(1, backend.Activations);
        }

        [TestCase(0, "GAME_VIEW_NOT_OPEN")]
        [TestCase(2, "GAME_VIEW_AMBIGUOUS")]
        public void MissingOrAmbiguousGameViewDoesNotActivate(int count, string code)
        {
            var backend = new Backend();
            backend.State.gameViewCount = count;
            Assert.AreEqual(code, (string)Run(backend, true)["code"]);
            Assert.Zero(backend.Activations);
            Assert.Zero(backend.Focuses);
        }

        [Test]
        public void GameViewFocusRunsOnlyAfterForegroundAndIsVerifiedLater()
        {
            var backend = new Backend { Tick = (n, b) =>
            {
                if (n >= 2) b.State.foregroundPid = 42;
                if (n >= 4) b.State.gameViewFocused = true;
            } };
            var result = Run(backend, true);
            Assert.IsTrue((bool)result["success"]);
            Assert.AreEqual(1, backend.Activations);
            Assert.AreEqual(1, backend.Focuses);
            Assert.AreEqual(5, backend.Updates);
            Assert.IsTrue((bool)result["data"]["gameViewFocusVerified"]);
        }

        [Test]
        public void GameViewRequestSuccessWithoutKeyboardFocusDoesNotPass()
        {
            var backend = new Backend();
            backend.State.foregroundPid = 42;
            Assert.AreEqual("GAME_VIEW_FOCUS_TIMEOUT", (string)Run(backend, true)["code"]);
            Assert.AreEqual(1, backend.Focuses);
        }

        [Test]
        public void LosingForegroundAfterGameViewRequestDoesNotReclaimIt()
        {
            var backend = new Backend { Tick = (n, b) => { if (n == 2) b.State.foregroundPid = 7; } };
            backend.State.foregroundPid = 42;
            Assert.AreEqual("EDITOR_FOREGROUND_LOST", (string)Run(backend, true)["code"]);
            Assert.AreEqual(1, backend.Focuses);
            Assert.Zero(backend.Activations);
        }

        [Test]
        public void IdentityChangeStopsWithoutFurtherRequests()
        {
            var backend = new Backend { Tick = (n, b) => b.State.projectPath = "/other" };
            Assert.AreEqual("EDITOR_IDENTITY_CHANGED", (string)Run(backend, true)["code"]);
            Assert.Zero(backend.Focuses);
        }

        [Test]
        public void ModalAppearingDuringVerificationStops()
        {
            var backend = new Backend { Tick = (n, b) => b.State.modal = true };
            Assert.AreEqual("EDITOR_MODAL_BLOCKED", (string)Run(backend)["code"]);
            Assert.AreEqual(1, backend.Activations);
        }

        [Test]
        public void RejectedWindowRequestStops()
        {
            var backend = new Backend { FocusAllowed = false };
            backend.State.foregroundPid = 42;
            Assert.AreEqual("GAME_VIEW_FOCUS_REFUSED", (string)Run(backend, true)["code"]);
            Assert.AreEqual(1, backend.Focuses);
        }

        [Test]
        public void BothCommandsAreMutatingAndAvailableInMinimalAndCoreProfiles()
        {
            foreach (var name in new[] { "focus_unity_editor", "focus_game_view" })
            {
                Assert.IsNotNull(ToolRegistry.GetMethod(name));
                Assert.IsFalse(ToolRegistry.IsReadOnly(ToolRegistry.GetMethod(name)));
                CollectionAssert.Contains(MCPToolExportPolicy.DefaultMinimalTools.ToArray(), name);
                CollectionAssert.Contains(MCPToolExportPolicy.DefaultCoreTools.ToArray(), name);
            }
        }

        [UnityTest, Explicit("Owned interactive disposable project only; test temporarily backgrounds this Editor.")]
        public IEnumerator NativeFocusCommandsVerifyActualForegroundAndGameView()
        {
            if (Application.isBatchMode ||
                (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.OSXEditor))
                Assert.Ignore("Requires an interactive Windows/macOS Editor.");
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Assert.IsNotNull(type);
            var prior = Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType() == type).ToArray();
            if (prior.Length != 1) Assert.Ignore("Use a disposable layout with one existing Game View.");
            try
            {
                var backend = new NativeEditorFocus();
                var initial = backend.Capture();
                Assert.IsNull(initial.observationError);
                Assert.IsFalse(initial.modal, "Close any modal before this owned test.");
                Debug.Log("FOCUS_NATIVE_READY: backgrounding only this disposable Editor; no input is sent.");
                if (!NativeFocusTestWindow.BackgroundCurrentEditor())
                    Assert.Ignore("The OS refused the owned Editor's background request; focus commands were not tested.");
                var deadline = EditorApplication.timeSinceStartup + 10;
                EditorFocusState before = null;
                var priorForeground = 0;
                var stableSamples = 0;
                while (EditorApplication.timeSinceStartup < deadline)
                {
                    yield return null;
                    before = backend.Capture();
                    Assert.IsNull(before.observationError);
                    Assert.AreEqual(initial.editorPid, before.editorPid);
                    Assert.AreEqual(initial.projectPath, before.projectPath);
                    if (before.foregroundPid > 0 && before.foregroundPid != before.editorPid && !before.nativeWindowFocused)
                    {
                        stableSamples = before.foregroundPid == priorForeground ? stableSamples + 1 : 1;
                        priorForeground = before.foregroundPid;
                        if (stableSamples >= 2) break;
                    }
                    else { stableSamples = 0; priorForeground = 0; }
                }
                if (stableSamples < 2)
                    Assert.Ignore("No stable external foreground was observed; focus commands were not tested.");
                Debug.Log("FOCUS_NATIVE_PRECONDITION: " + Json(before));
                var app = EditorFocusFunctions.FocusUnityEditor();
                while (!app.IsCompleted) yield return null;
                var appResult = Json(app.GetAwaiter().GetResult());
                Debug.Log("FOCUS_NATIVE_EDITOR_RESULT: " + appResult);
                Assert.IsTrue((bool)appResult["success"], appResult.ToString());
                var game = EditorFocusFunctions.FocusGameView();
                while (!game.IsCompleted) yield return null;
                var gameResult = Json(game.GetAwaiter().GetResult());
                Assert.IsTrue((bool)gameResult["success"], gameResult.ToString());
                Assert.IsTrue((bool)gameResult["data"]["after"]["gameViewFocused"]);
                Debug.Log("FOCUS_NATIVE_VERIFIED: " + gameResult);
            }
            finally
            {
                // Cleanup is separate from the assertions and cannot turn an
                // ignored/failed run into evidence that the commands passed.
                NativeFocusTestWindow.RestoreCurrentEditorForCleanup();
            }
        }
    }
}
