<p align="center">
  <h1 align="center">KitWright MCP for Unity</h1>
  <p align="center">
    <strong>The Most Advanced MCP Server for Unity Editor</strong>
  </p>
  <p align="center">
    <a href="#"><img src="https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity" alt="Unity 2022.3+"></a>
    <a href="#"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT"></a>
    <a href="#"><img src="https://img.shields.io/badge/MCP-Compatible-green" alt="MCP Compatible"></a>
    <a href="#"><img src="https://img.shields.io/badge/Platform-Editor%20Only-orange" alt="Editor Only"></a>
  </p>
  <p align="center">
    <a href="./README_CN.md">中文</a> | English
  </p>
  <p align="center">
    <img src="./Documentation~/Video_Logo.gif" alt="The Most Advanced MCP Server for Unity" width="60%">
  </p>
</p>

> 💖 If you find this project useful, please consider giving it a Star. It helps more Unity developers discover it and supports ongoing development.

---

This is the maintained `amynoox/unity-mcp` fork of KitWright MCP for Unity, under the original MIT license. Install and update this fork through reviewed project package files as described below.

KitWright MCP for Unity is an MIT-licensed Unity Editor MCP server that lets AI assistants like Claude Code, Cursor, LM Studio, Windsurf, Codex, and VS Code Copilot operate directly inside your running Unity project.

Describe your game in one sentence — your AI assistant builds it in Unity through KitWright MCP for Unity's 274 built-in tools for scene creation, script generation, runtime validation, input simulation, performance analysis, and editor automation.

> *"Build a snake game with a 10x10 grid, food spawning, score UI, and game-over screen"*
>
> Your AI assistant handles it through KitWright MCP for Unity: creates the scene, generates all scripts, sets up the UI, and configures the game logic — all from a single prompt.

<p align="center">
  <img src="./Documentation~/demo.gif" alt="KitWright MCP for Unity — demo" width="100%">
</p>
<p align="center"><em>Demo — AI searches project prefabs and builds a city scene through MCP tools. <a href="https://github.com/kitwright/unity-mcp/raw/main/Documentation~/demo.mp4">Watch HD MP4</a>.</em></p>

## Quick Start

### Verified Editor focus

On an owned, interactive Windows or macOS Editor, use `focus_unity_editor` to
request foreground activation of the connected Editor process, then
`focus_game_view` to focus its existing Game View. Both are included in the
minimal and core tool profiles. They target only the connected Editor, accept
`timeout_ms` (100–5000, default 2000), and verify the result on two later Editor
updates. `get_editor_state` also includes the current native `focus` observation.

A successful menu invocation or activation request alone is not success. The
commands report OS refusal, unavailable/headless desktops, modal or busy Editors,
missing/ambiguous Game Views, and verification timeouts as structured failures.
They never unlock a session, change permissions, inject a click/key, start Play
Mode, create another window, or retry activation in a loop. After a transport
timeout, inspect current state before deciding whether an operation may be
retried. Focus evidence does not prove that a game consumed input; perform the
separate owned input check before live QA.

macOS may refuse an Editor's request to activate itself. In that case, use the
authorized Computer Use connection to bring the **same existing Unity window**
forward, verify the connected project and process again, and call the focus tools.
This is the shared no-launch recovery path: no application-opening API, replacement
Editor, permission change, or authentication automation. An OS-refused native
attempt remains a failure; a later Computer Use-assisted result is recorded
separately. If desktop access is unavailable, report that blocker rather than
claiming focus or readiness.

If you just want to get connected fast, do these three things:

- Install the Unity package from the Git URL
- Open `Window > KitWright > MCP Window` and start the server from the **Server** tab
- Use the built-in one-click client configuration

### 1. Install via UPM (Git URL)

In Unity, go to **Window → Package Manager → + → Add package from git URL**:

```
https://github.com/amynoox/unity-mcp.git#main
```

> 💡 Before you clone or install, a quick ⭐ on GitHub would be greatly appreciated.

### Updating this fork

This repository is the maintained fork at `amynoox/unity-mcp`. Install it from
the Git URL above; the upstream OpenUPM and Asset Store distributions do not
contain this fork's changes.

The built-in release checker and installer are disabled. Update through reviewed,
versioned project package files. For The Ranchers QA, both package URL fields use
`https://github.com/amynoox/unity-mcp.git#main`, while the concrete hash in
`Packages/packages-lock.json` committed in game master selects the approved revision.
A new MCP commit requires a dedicated game maintenance Draft PR and human merge
before workers adopt it at an owned idle checkpoint. Workers verify the package
actually loaded and keep the same game/package snapshot throughout the test and
on every helper. They never independently refresh main or edit package caches.

### 2. Start the MCP Server

**Menu: Window → KitWright → MCP Window**, then start the server from the **Server** tab.

The server starts on `http://127.0.0.1:8765/` by default.

**Broker Mode** is the default transport. It runs a tiny local broker with Unity's bundled Mono, keeps the same `127.0.0.1` port for MCP clients, requires no client config change, and holds the connection across Unity script recompiles and Play Mode domain reloads. If the broker cannot start, the server falls back to direct in-process HTTP automatically; you can also turn broker mode off in the **Server** tab to use direct HTTP always.

The window has five tabs: **Server**, **Settings**, **Skills**, **Tool Exposure**, and **Integrations**.

Open the **Tool Exposure** tab if you want to edit the exact tools exposed by `core` or `full`.

Open the **Settings** tab if you need to adjust `execute_code` safety defaults or plugin debug logging.

### 3. Configure Your AI Client

Use the built-in **One-Click MCP Configuration** in the **Server** tab first.

Select your target client, click **Configure**, and the package writes the recommended MCP config entry for you.

For Claude Code, Cursor, and Codex, click **Configure + Skills** to also install the default project MCP workflow skill.

If you want project-specific AI guidance for the current Unity project, open the **Skills** tab to choose supported platforms and install the default `unity-mcp-workflow` skill.

If you prefer to edit config files manually, use the examples below as fallback references:

<details>
<summary>Claude Code / Claude Desktop</summary>

```json
{
  "mcpServers": {
    "kitwright": {
      "type": "http",
      "url": "http://127.0.0.1:8765/"
    }
  }
}
```

</details>

<details>
<summary>Cursor</summary>

```json
{
  "mcpServers": {
    "kitwright": {
      "url": "http://127.0.0.1:8765/"
    }
  }
}
```

</details>

<details>
<summary>LM Studio</summary>

LM Studio is not one of the one-click targets — its `mcp.json` location varies by version and platform. Open **Program > Install > Edit mcp.json** in LM Studio and paste the entry below.

```json
{
  "mcpServers": {
    "kitwright": {
      "url": "http://127.0.0.1:8765/"
    }
  }
}
```

</details>

<details>
<summary>VS Code</summary>

```json
{
  "servers": {
    "kitwright": {
      "type": "http",
      "url": "http://127.0.0.1:8765/"
    }
  }
}
```

</details>

<details>
<summary>Trae</summary>

```json
{
  "mcpServers": {
    "kitwright": {
      "url": "http://127.0.0.1:8765/"
    }
  }
}
```

</details>

<details>
<summary>Kiro</summary>

```json
{
  "mcpServers": {
    "kitwright": {
      "type": "http",
      "url": "http://127.0.0.1:8765/"
    }
  }
}
```

</details>

<details>
<summary>Codex</summary>

```toml
[mcp_servers.kitwright]
url = "http://127.0.0.1:8765/"
```

</details>

<details>
<summary>Windsurf</summary>

Use the same JSON structure as Cursor unless your local Windsurf version requires a different MCP config format.

</details>

### 4. Verify the Connection

Open your AI client and try a few safe requests first:

- "Call `get_scene_info` and tell me what scene is open."
- "Read `unity://project/context` and summarize the current editor state."
- "Use `execute_code` to return the active scene name."

If those work, the MCP server, resources, and primary execution tool are connected correctly.

### 5. Start Building

Open your AI client and try: *"Create a 3D platformer level with 5 floating platforms"*

## Before You Start

- This package is **Editor-only**. It does not add runtime components to your built game.
- The MCP server starts on `http://127.0.0.1:8765/` by default.
- Local MCP server settings are stored in `UserSettings/KitWrightMcpSettings.json`.
- The package defaults to the `core` MCP tool profile to reduce tool-list noise for AI clients. `core` currently exposes 43 high-signal tools centered on `execute_code`, play mode control, input simulation, screenshots, performance inspection, logs, compilation checks, structured object location and component editing, editor selection / prefab-stage state, live C# API reflection, Unity documentation lookup, and `execute_menu_item` as a low-friction fallback. Switch to `full` in the **Server** tab if you want all 274 tools exposed.
- `execute_code` safety checks and the stricter filesystem guard are enabled by default from the **Settings** tab. The guard blocks obvious destructive snippets, broad `System.IO` writes, raw file streams, and absolute/user/system/traversal paths. It catches accidents, not intent: `safety_checks` is a tool argument, so any client that can call `execute_code` can pass `safety_checks=false` and skip both the source blocklist and the compiled-assembly guard. It is neither a sandbox nor a security boundary — see [SECURITY.md](./SECURITY.md).
- Plugin debug logging is off by default and can also be enabled from the **Settings** tab. Warnings and errors are always written to the Unity Console.
- All exposed MCP tools run directly. There is no extra approval toggle.
- Package updates use reviewed project package files; this fork does not check upstream releases or install updates from the MCP window.

## Why This Project

- **`execute_code` First** — Optimized around one in-memory C# execution tool for rich editor/runtime orchestration. See [`execute_code`: In-Memory C# Execution](#execute_code-in-memory-c-execution) below for details.
- **Default Safety Checks** — `execute_code` now has persistent default-on safety toggles, including a stricter filesystem guard for clients that do not expose per-call arguments clearly
- **Play Mode Automation** — Enter play mode, simulate keyboard/mouse input, capture screenshots, inspect logs, and validate behavior from the same MCP session
- **Project Context Built In** — Exposes live resources for project state, active scene, selection, compilation, console output, and MCP interaction history
- **Focused by Default, Full When Needed** — `core` exposes a compact high-signal toolset; `full` exposes 274 tools
- **Single Unity Package** — No extra approval UI, no external daemon to click through, and no Python requirement for the Unity-side plugin itself
- **Extensible** — Add custom tools with attribute-based discovery, or connect Unity to external MCP services when needed

## Highlights

- **274 Built-in Tools** — Scene editing, assets, scripts, play mode control, screenshots and screenshot diffing, performance analysis, prompts, resources, structured object location, SerializedObject-based component and project-settings editing, animator authoring, compound uGUI controls, editor-state inspection, menu-item fallback, and editor automation across 57 modules. Full list: [TOOLS.md](TOOLS.md)
- **Structured Returns + `instanceId` Chaining** — Tools return `{success, message, data}` JSON with stable `instanceId` fields so agents can chain `by_id` calls reliably instead of re-resolving by name
- **`IKitWrightCommand` for `execute_code`** — New snippet template with auto-Undo (`ctx.RegisterObjectCreation` / `ctx.RegisterObjectModification` / `ctx.DestroyObject`), structured logs (`ctx.Log/LogWarning/LogError`), and a tracked changelog returned to the agent
- **Resources & Prompts** — Live project context, scene/selection/error resources, resource templates, and reusable workflow prompts
- **Input Simulation + Screenshots** — Drive play mode with keyboard/mouse simulation and verify results with game/scene captures
- **Project-managed Updates** — The consuming project selects and records the approved fork revision; active tests retain it
- **Integrations Tab** — Detects Hot Reload, Memory Profiler, Addressables, Input System, Timeline, URP, and Test Framework, and shows which tools each one unlocks
- **One-Click Client Configuration** — Write MCP config entries for 19 targets directly from the Unity window: Claude Code, Cursor, VS Code (+ Insiders), Codex, Windsurf, Cline, Kiro, Trae, Rider (+ Junie), Kimi Code, Qwen Code, Antigravity, Kilo Code, OpenCode, GitHub Copilot CLI, CodeBuddy CLI, and Roo Code
- **Tool Exposure Control** — Edit the exact tools exposed by `core` and `full`
- **Project Skills Manager** — Configure project-level skills for supported AI clients, currently installing the default `unity-mcp-workflow` skill
- **MCP Settings** — Adjust `execute_code` safety defaults and enable verbose plugin debug logging when troubleshooting MCP connections or tool execution
- **Vendor Agnostic** — Works with any AI client that supports MCP: Claude Code, Cursor, LM Studio, Windsurf, Codex, VS Code Copilot, etc.

## `execute_code`: In-Memory C# Execution

`execute_code` is the heart of KitWright MCP for Unity. It lets an AI write a C# snippet, compile it through a Roslyn-first in-memory flow, and run it on the editor thread — the agent gets the full Unity Editor and runtime API surface without writing any project files to disk.

- **Zero project footprint compilation** — Snippets are compiled with Unity's bundled Roslyn csc first while preserving the in-memory compilation/execution flow. No `.cs` files are written under `Assets/`, no domain reload is triggered, no project state is touched beyond what the snippet itself does.
- **Editor-ready before it runs** — Each call refreshes the AssetDatabase and waits for any pending compilation to settle before compiling the snippet, so external file edits are picked up automatically without a separate `request_recompile`.
- **Auto-Undo + structured logs (recommended template)** — Implement `IKitWrightCommand` and use the injected `ExecutionContext` so every created / modified / destroyed object participates in editor Undo, and the changelog is returned to the agent.

```csharp
using UnityEngine;
using UnityEditor;
using KitWright.Editor.Tools.Helpers;
using KitWright.Editor.Tools.Scripting;

public class CommandScript : IKitWrightCommand
{
    public void Execute(ExecutionContext ctx)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ctx.RegisterObjectCreation(go);          // auto-Undo + tracked
        ctx.Log("Created {0}", go.name);
        ctx.ReturnValue = GameObjectSerializer.Describe(go, includeComponents: false);
    }
}
```

The response carries `{ logs, created, modified, destroyed, returnValue }`, so the agent can verify exactly what changed without re-querying the scene.

The legacy template (`public static string Run()`) is still supported — useful for one-off inspection snippets where structured tracking is overkill.

**When to reach for `execute_code` vs a specialized tool** — `execute_code` shines for multi-step orchestration, novel reads, and situations where chaining 5–10 narrow tool calls would be noisier than one snippet. For single-field component edits, simple selection changes, or anything covered by an existing tool, prefer the dedicated tool — it is cheaper for the LLM to call and easier to verify.

## Comparison With Coplay

The table below compares this repository with the publicly documented behavior of Coplay's open-source `unity-mcp` repository on GitHub (checked against v10.0.0 and the public docs site).

| Area | KitWright MCP for Unity | Coplay `unity-mcp` |
|------|--------------------------|--------------------|
| Unity-side architecture | Embedded Unity Editor package with built-in HTTP MCP server | Unity bridge plus a Python MCP server, run locally or remote-hosted with API-key auth |
| Extra local prerequisites | Unity package only for core workflows | Unity + Python 3.10+ + `uv` according to the public quick start |
| Tool surface | 274 fine-grained tools across 57 modules, chained by `instanceId` | ~51 wide `manage_*` entrypoints with an `action` enum per tool (README states "47 focused entrypoints") |
| Primary workflow style | `execute_code` first, then focused helper tools | `manage_*` families first; `execute_code` is available in the `scripting_ext` group |
| Default tool exposure | Compact `core` profile with optional `full` expansion | Tool groups toggled per session via `manage_tools` |
| Asset generation | Not built-in (compose external APIs via `execute_code`) | `generate_image` / `generate_audio` / `generate_model` via fal.ai, Tripo, Meshy, plus Sketchfab import |
| UI Toolkit and ProBuilder | Not built-in (uGUI creation tools only) | `manage_ui` for UXML/USS/UIDocument and `manage_probuilder` for in-editor modeling |
| Built-in context model | Project resources, resource templates, workflow prompts, interaction history | Public README emphasizes tool families and bridge/server workflow |
| Play mode validation | Built-in play mode control, screenshots, logs, and input simulation in the package | Public README emphasizes broad Unity management and automation tools |
| C# API introspection | `reflect_api` — declared members, full signatures per member, extension methods, obsolete members, interfaces, and `search` by substring scoped to `unity` / `packages` / `project` / `all`. Adds candidate suggestions when a type or member name does not resolve, case-insensitive member matching, and `include_non_public` for private and internal members | `unity_reflect` — `get_type` / `get_member` / `search` with a comparable scope filter, extension-method resolution, and interface and obsolete-member listings |
| Documentation | `README.md` plus [TOOLS.md](TOOLS.md), both generated from the `[ToolProvider]` sources | Auto-generated docs site with a detail page per tool covering parameters, actions, and examples |
| Tool exposure switching | `set_tool_profile` switches `minimal` / `core` / `extended` / `full` from the client and pushes `tools/list_changed` | `manage_tools` enables and disables tool groups per session |
| Tool annotations | `annotations.readOnlyHint` on read-only tools | `readOnlyHint` and `destructiveHint` on annotated tools |
| Positioning | Lightweight, direct, MIT-licensed Unity MCP server for AI-driven editor control | Full-featured Unity bridge maintained by Coplay with Python-backed server setup |

Source for Coplay column: [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) and its [tool reference](https://coplaydev.github.io/unity-mcp/reference/tools)

## Comparison With Unity AI Assistant

The table below compares this repository with Unity Technologies' official `com.unity.ai.assistant` package (v2.7.0-pre.2 as of 2026-05).

| Area | KitWright MCP for Unity | Unity AI Assistant |
|------|--------------------------|--------------------|
| Minimum Unity version | 2022.3 | 6000.3 (Unity 6 only) |
| License | MIT, open source | Unity Terms of Service, proprietary |
| Deployment | Local HTTP MCP server in Editor, no cloud | Editor + native Relay subprocess + Unity Cloud backend |
| Billing | Free, user brings their own AI client | Credits-based (Unity Dashboard) |
| Tool exposure | 274 tools across 57 modules, `core` (43) / `full` profiles | ~15 MCP tools (mostly `Manage*` families) |
| Generic escape hatch | `execute_code` — Roslyn-first in-memory compile, `IKitWrightCommand` + Undo, no sandbox (client-side approval) | `RunCommand` — namespace blacklist sandbox |
| Play mode validation | Full loop: enter / simulate input / capture / read logs / exit | Enter/Exit only; no input simulation |
| Asset generators | Not built-in (compose external APIs via `execute_code`) | Native Image / Mesh / PBR / Sound / Animation generators |
| Primary client model | BYO any MCP client (Claude Code / Cursor / LM Studio / Codex / VS Code) | Built-in chat window + ACP for Claude/Gemini via Gateway |
| Offline-capable | Yes for tool calls (inference depends on chosen client) | No (inference requires Unity Cloud) |

For a long-form comparison of the two approaches see [KitWright MCP for Unity vs Unity AI Assistant detailed comparison](https://blog.csdn.net/m0_62670368/article/details/161039766) (Chinese).

## MCP Capabilities

The current open-source package exposes four high-value capability layers:

- **Tools** — 274 total tools in `full`, 43 focused tools in `core`
- **Primary execution** — `execute_code` for rich editor/runtime orchestration
- **Prompts** — workflow prompts like `fix_compile_errors`, `runtime_validation`, and `create_playable_prototype`
- **Resources** — project context, scene summaries, selection state, compile errors, console errors, MCP interaction history, plus resource templates for scene objects, components, and asset paths

## Built-in Tools

<!-- tools-summary:start -->
KitWright MCP for Unity ships **275 tool functions across 58 modules** (`core` profile exposes 55 of them).

| Module | Tools | Module | Tools |
|--------|-------|--------|-------|
| **EditorState** | 18 | **Texture** | 4 |
| **GameObject** | 14 | **Build** | 3 |
| **Profiler** | 13 | **ComponentBatch** | 3 |
| **Scene** | 13 | **ComponentProperty** | 3 |
| **Animation** | 10 | **Docs** | 3 |
| **Prefab** | 10 | **Lighting** | 3 |
| **Asset** | 8 | **Package** | 3 |
| **Terrain** | 8 | **Physics** | 3 |
| **AssemblyDefinition** | 7 | **ScriptableObject** | 3 |
| **InputSimulation** | 7 | **Sprite** | 3 |
| **Prefs** | 7 | **Testing** | 3 |
| **SpriteAtlas** | 7 | **Undo** | 3 |
| **UI** | 7 | **AssetImport** | 2 |
| **Visual** | 7 | **EditorDialog** | 2 |
| **Addressable** | 6 | **EditorFocus** | 2 |
| **Audio** | 6 | **EditorWindowInteraction** | 2 |
| **Code** | 6 | **Material** | 2 |
| **InputActions** | 6 | **Performance** | 2 |
| **NavMesh** | 6 | **ProjectSettings** | 2 |
| **Shader** | 6 | **References** | 2 |
| **File** | 5 | **Batch** | 1 |
| **MemorySnapshot** | 5 | **Hierarchy** | 1 |
| **SceneView** | 5 | **Interop** | 1 |
| **Screenshot** | 5 | **MenuItem** | 1 |
| **Volume** | 5 | **Mesh** | 1 |
| **Camera** | 4 | **Particle** | 1 |
| **Compilation** | 4 | **Reflection** | 1 |
| **LodConstraint** | 4 | **Timeline** | 1 |
| **Script** | 4 | **ToolExposure** | 1 |

> 📖 Every tool with its description: [TOOLS.md](TOOLS.md).
<!-- tools-summary:end -->

> 📊 See [PROFILER_TOOLS.md](PROFILER_TOOLS.md) for the full Profiler tool reference, implementation notes, known limitations, and test report.

## Adding Custom Tools

Your project can declare its own MCP tools with the same attributes the built-in tools use. Put the class in an **Editor** assembly — `KitWright.Editor` is `autoReferenced`, so no asmdef reference setup is needed:

```csharp
using System.ComponentModel;
using KitWright.Editor.Tools;

[ToolProvider("MyTools")]
public static class MyCustomTools
{
    [Description("Spawns enemies at random positions in the scene")]
    public static string SpawnEnemies(
        [ToolParam("Number of enemies to spawn", Required = true)] int count,
        [ToolParam("Prefab path in Assets")] string prefabPath)
    {
        // Your implementation here
        return $"Spawned {count} enemies";
    }
}
```

Public static methods on the class are discovered automatically, converted to snake_case (`spawn_enemies`), and exposed via MCP with JSON Schema definitions generated from the parameter list. Mark a method `[ReadOnlyTool]` when it does not modify the scene or project, and clients receive `annotations.readOnlyHint` for it.

Project-declared tools are exposed under **every** profile, including the default `core`, since writing one is already an explicit opt-in — a custom tool would otherwise be invisible to the profile most clients connect with. They still appear in the **Tool Exposure** tab, so a profile configured there can turn them off like any built-in tool.

### Asynchronous custom tools and loading budgets

Return `System.Threading.Tasks.Task` or `Task<T>` from asynchronous custom tools.
The dispatcher awaits that task before returning its final value. UniTask operations
may still be awaited inside the method, or converted once with `AsTask()` at the
MCP boundary; returning a UniTask directly does not make the dispatcher await it.

Project and third-party tools returning `Task`/`Task<T>` receive a **30-minute
(1800-second)** call budget by default, including slow Unity Editor game/session
loading. Ordinary synchronous tools and built-in tools retain their existing
budgets. `[LongRunningTool(seconds)]` is public and can set an explicit per-method
budget; the usual server/transport fallback still applies when it is larger.
The existing broker holds requests for 32 minutes, so keep tool budgets at or below
30 minutes and call separate long loading steps individually.

This is a waiting budget, not an artificial delay or a guarantee that the game is
ready. A tool must await its actual completion condition before returning success.
The external MCP client must also permit the longer call. A disconnected client or
timeout does not cancel an already-started game operation: inspect its current
state before retrying a save creation, session start, teleport or other mutation.

## Architecture

```
MCP Server (HTTP JSON-RPC 2.0)
    └─ MCPRequestHandler (protocol handling)
        └─ MCPExecutionBridge
            └─ FunctionInvoker (reflection-based invocation)
                └─ Tool Functions (274 built-in tools across 57 modules)
```

```
External AI Client → HTTP Request → MCPRequestHandler → MCPExecutionBridge → FunctionInvoker → tool method
```

## Requirements

- Unity 2022.3 or later
- `com.unity.nuget.newtonsoft-json` — pulled in automatically by UPM and OpenUPM installs; Asset Store imports offer it in the Package Manager dependency dialog
- `com.unity.ugui` and `com.unity.test-framework` — declared dependencies, already present in a default Unity project

## Contributing

Contributions are welcome! Please read the [Contributing Guide](CONTRIBUTING.md) before submitting a PR.

## Security

Found a vulnerability? Report it privately — see the [Security Policy](SECURITY.md).

## License

[MIT](LICENSE) — Free to use, modify, distribute, and integrate into commercial or open-source projects.

The KitWright name and logo are trademarks of the KitWright project and are **not** covered by the MIT license. You may not use the name or logo to brand derivative works or imply endorsement without prior written permission. All rights to the brand assets (files under `Editor/Icons/`) are reserved.

