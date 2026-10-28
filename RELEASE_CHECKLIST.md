# Release Checklist

Use this checklist before publishing a release of the `amynoox/unity-mcp` fork.

The local helper `scripts~/release.sh <version>` prepares Unity artifacts, validates
their paths and generates hashes/notes. Supply an explicitly authorized idle test
project through `UNITY_PROJECT_ROOT`; the helper does not select a game checkout.
Preparation and publication are separate. Version changes must go through a Draft
PR and human merge first. `--publish-github` accepts only clean tracked files at
the exact remote main commit with that version and creates a Draft GitHub Release.
It never force-pushes a branch or overwrites an existing tag/release.

The fork is consumed through Git. NuGet, OpenUPM and the old wrapper-registry
options are refused before any mutation. The inherited MCP Registry workflow is
retired: it described an upstream wrapper that this fork does not distribute.
A Registry listing requires its own reviewed distribution and metadata before
being added here. Tags trigger no Registry publication. A hosted run still needs
the explicit human authorization below; do not invoke one for QA package adoption.

## 1. Repository Hygiene

- [ ] `git status` is clean except for the files intended for the release
- [ ] No local junk is present (`.idea/`, `.DS_Store`, temporary exports, local test files)
- [ ] `package.json` version matches the intended release
- [ ] `CHANGELOG.md` includes the release notes for the target version
- [ ] `README.md`, `README_CN.md`, and `Documentation~/index.md` match the current product behavior

## 2. Unity Smoke Test

- [ ] Test in a clean Unity `2022.3+` project
- [ ] Install the package from the Git URL
- [ ] Open `Window > KitWright > MCP Window`
- [ ] Start the MCP server successfully from the **Server** tab
- [ ] Confirm the configured local endpoint is reachable from an MCP client
- [ ] If the configured port is already in use, verify the server reports the startup failure clearly
- [ ] Run a read-only tool such as `get_scene_info`
- [ ] Run a scene-changing tool such as `create_game_object`
- [ ] Verify interaction logs appear in the MCP Server window
- [ ] Trigger a script recompile and confirm the MCP server recovers correctly

### Tests requiring an interactive Editor

Run qualification on the available local LAN Windows and macOS machines and
record the exact source revision and test environment. Headless fixture runs lack
a graphics device, screen and interactive editor loop. Tests that need those must
also run once per release in an editor that has what they need:
open a project with both packages as testables, then run the EditMode suite from the Test Runner
window. Batchmode will not do, since it skips these tests too.

- [ ] `ScreenshotFunctionsTests`: capture read-back (graphics device)
- [ ] `EditorWindowInteractionFunctionsTests`: UI Toolkit click (rendered view)
- [ ] `EditorThreadHelperStallTests.SinceLastPump_IsFreshWhileTheEditorIsRunningThisTest` (editor loop ticking)
- [ ] `ShaderFunctionsTests.ListShaders_CountCapReportsThePreCapTotalAndTheShownCount` (a project with two or more shaders)
- [ ] `HotReloadInteropTests` (SingularityGroup Hot Reload installed), and the Volume tools' tests (URP installed)
- [ ] `InputSimulationFunctionsTests` mouse drag, touch and gamepad (a Game View to route device input to)
- [ ] `PowerThrottlingTests` (Windows)
- [ ] `EditorFocusFunctionsTests.NativeFocusCommandsVerifyActualForegroundAndGameView` (owned interactive Windows; unassisted foreground and Game View focus; record macOS refusal separately)
- [ ] `EditorFocusFunctionsTests.NativeFocusCommandsVerifyExistingForegroundAndGameView` (owned interactive macOS and Windows; authorized Computer Use first focuses the existing Editor, then both native tools verify focus)

The opt-in focus test temporarily minimizes (Windows) or hides (macOS) only its
own disposable Editor, then requires two real observations of another foreground
process before testing the production commands. It does not depend on a timed
click in another application. Keep the READY, PRECONDITION, EDITOR_RESULT and
VERIFIED records with the native result. An OS refusal, missing precondition or
ignored result is not a pass. Cleanup may restore the same Editor but cannot
change the recorded test outcome. This fixture proof is separate from calling
the configured MCP tools and observing physical game input in the pilot.
The existing-foreground test is a distinct recovery-path test. It does not hide
the Editor or claim native activation from a background state. Preserve both its
Computer Use preparation evidence and native results, alongside prior refusals.

The production workflow records native request return values separately from
observed focus. It makes one request and uses the same bounded verification even
when that request returns false; it never retries activation or accepts a request
return as proof. A real refusal with no verified focus still fails.

On macOS the request restores only this Editor if hidden, uses normal AppKit
self-activation and makes its existing main/key window key. It never changes
activation policy, targets another application, launches an Editor, or requests
an OS security exception. The snapshot records main-thread, hidden and activation
policy state for diagnosis. Modern macOS may decline cooperative activation;
that remains a failed focus result unless later native samples prove otherwise.
Hidden-window lookup occurs after the unhide/activation request; temporarily absent
main/key windows do not short-circuit that request. Success still requires the
same later native key-window observations, not merely an issued unhide request.
- [ ] Add-on: `UIReconstructToolsTests`, the whole match pipeline (graphics device), and `RuntimeToolsPlayModeTests` drag and scroll (a screen larger than 640x480)

Record all skips and their actual environmental reasons; never count them as
passes. GitHub Actions may be used only after explaining why LAN machines are
unsuitable and obtaining explicit human permission. If that exception is granted,
the hosted skip-reporting checks still apply.

## 3. MCP Client Verification

- [ ] Verify at least one primary client can connect (`Claude Code`, `Cursor`, `Codex`, etc.)
- [ ] Confirm `tools/list` returns the expected tool set
- [ ] Confirm a tool call succeeds end-to-end from the external client
- [ ] Verify the one-click config output still matches the documented config snippets

## 4. Docs and API Surface

- [ ] Custom tool examples use `[ToolProvider]`
- [ ] Any newly added tool has a clear `[Description]` and `[ToolParam]` metadata
- [ ] Any user-visible behavior change is documented

## 5. GitHub Release Readiness

- [ ] Full native Windows and macOS suites passed at the same committed source; logs and skip reasons retained
- [ ] PR checklist still reflects the current release process
- [ ] License file is present and correct
- [ ] Repository description/topics are set on GitHub
- [ ] Initial tags or release tags follow the chosen versioning scheme
- [ ] The `.unitypackage` was exported from an explicit filtered asset list, without `IncludeDependencies` or `IncludeLibraryAssets`
- [ ] The `.unitypackage` contains only `Assets/unity-mcp` paths; verify there are no `ProjectSettings/`, `Packages/`, `Library/`, `Tests/`, `CLAUDE.md`, or local token entries before upload

## 6. Publish

- [ ] Commit the release changes with a clear release-oriented message
- [ ] Create the Draft GitHub Release from the reviewed main commit
- [ ] Have the human validate and publish that release/tag; preserve existing tags/releases
- [ ] Verify the public repository renders the README and package documentation correctly

## 7. Post-Release

- [ ] Re-test installation from the public Git URL
- [ ] Check the GitHub release page for broken links or formatting issues
- [ ] Announce the release where appropriate
