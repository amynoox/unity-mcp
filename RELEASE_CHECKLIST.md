# Release Checklist

Use this checklist before publishing a new open-source release of KitWright MCP for Unity.

The local helper `scripts~/release.sh <version>` automates the high-risk mechanical steps:
version bumping, Unity EditMode tests, `.unitypackage` export, pathname validation, SHA/manifest generation,
wrapper packing, and opt-in publishing flags for GitHub, NuGet, and the MCP Registry.

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
- [ ] `EditorFocusFunctionsTests.NativeFocusCommandsVerifyActualForegroundAndGameView` (owned interactive Windows and macOS; actual foreground and Game View focus)
- [ ] Add-on: `UIReconstructToolsTests`, the whole match pipeline (graphics device), and `RuntimeToolsPlayModeTests` drag and scroll (a screen larger than 640x480)

Record all skips and their actual environmental reasons; never count them as
passes. GitHub Actions may be used only after explaining why LAN machines are
unsuitable and obtaining explicit human permission. If that exception is granted,
the hosted skip-reporting checks still apply.

Keep batch and interactive phases independent. A batch run can exit nonzero for
an inconclusive fixture even when it has no failed tests. Retain its exit code,
XML and omission reasons; do not rename it a pass. After reviewing that outcome
and the current ownership/preservation guards, run the separately authorized
interactive focus check with a new phase receipt. Never replay a completed batch
run just to reach the interactive phase or clear an unrelated fixture omission.

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
- [ ] Create and push the release tag
- [ ] Create the GitHub Release notes
- [ ] Verify the public repository renders the README and package documentation correctly

## 7. Post-Release

- [ ] Re-test installation from the public Git URL
- [ ] Check the GitHub release page for broken links or formatting issues
- [ ] Announce the release where appropriate
