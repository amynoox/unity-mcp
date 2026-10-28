#!/usr/bin/env bash
set -Eeuo pipefail

REPO="amynoox/unity-mcp"
RELEASE_COMMIT=""
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_ROOT="${UNITY_PROJECT_ROOT:-}"
ARTIFACT_ROOT="${RELEASE_ARTIFACT_DIR:-$ROOT/../release-artifacts}"
VALIDATION_TMP=""

usage() {
  cat <<'EOF'
Usage: scripts~/release.sh <version> [options]

Local artifact helper for the amynoox/unity-mcp fork.

Default behavior:
  - prepare versioned Unity package artifacts in an explicitly selected test project
  - run local Unity EditMode tests and validate the exported package
  - generate release notes, SHA256SUMS and a release manifest

Publishing is opt-in:
  --publish-github     Create a Draft GitHub Release from the exact reviewed main
                       commit. Requires clean tracked files, matching version,
                       approved origin and no existing tag/release. No branch rewrite.
  NuGet, OpenUPM and wrapper-registry publishing are not configured for this fork.
  Registry publication needs a separately reviewed distribution for this fork.

Options:
  --no-tests           Skip Unity EditMode tests
  --no-export          Skip unitypackage export; validates an existing package if present
  --no-wrapper         Compatibility option; this fork does not publish a wrapper
  --dry-run            Print major actions without mutating files or publishing
  -h, --help           Show this help

Environment:
  UNITY_BIN            Full path to the Unity executable. Auto-detected on macOS if omitted.
  UNITY_PROJECT_ROOT   Explicit existing, owned idle test project for tests/export.
  RELEASE_ARTIFACT_DIR  Output directory; defaults beside the source checkout.
EOF
}

fail() {
  echo "release: $*" >&2
  exit 1
}

info() {
  echo "==> $*"
}

VERSION="${1:-}"
if [[ -z "$VERSION" || "$VERSION" == "-h" || "$VERSION" == "--help" ]]; then
  usage
  exit 0
fi
shift || true

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]] || fail "invalid version: $VERSION"

RUN_TESTS=1
EXPORT_PACKAGE=1
PUBLISH_GITHUB=0
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-tests) RUN_TESTS=0 ;;
    --no-export) EXPORT_PACKAGE=0 ;;
    --no-wrapper) : ;;
    --publish-github) PUBLISH_GITHUB=1 ;;
    --publish-nuget|--publish-registry|--verify-openupm)
      fail "$1 is not configured for this fork; no source, artifact or publication was changed" ;;
    --dry-run) DRY_RUN=1 ;;
    -h|--help) usage; exit 0 ;;
    *) fail "unknown option: $1" ;;
  esac
  shift
done

OUT_DIR="$ARTIFACT_ROOT/$VERSION"
PACKAGE_OUTPUT="$OUT_DIR/KitWright.UnityMcp.v$VERSION.unitypackage"
NOTES_FILE="$OUT_DIR/release-notes.md"
SHA_FILE="$OUT_DIR/SHA256SUMS.txt"
MANIFEST_FILE="$OUT_DIR/release-manifest.json"

run() {
  if [[ "$DRY_RUN" == 1 ]]; then
    printf '+'
    printf ' %q' "$@"
    printf '\n'
  else
    "$@"
  fi
}

run_unity_export() {
  if [[ "$DRY_RUN" == 1 ]]; then
    printf '+'
    printf ' %q' "$@"
    printf '\n'
    return
  fi

  local log_file="$OUT_DIR/unitypackage-export.log"
  local timeout_seconds="${UNITY_EXPORT_TIMEOUT_SECONDS:-180}"
  local grace_seconds="${UNITY_EXPORT_EXIT_GRACE_SECONDS:-5}"
  local pid
  local elapsed=0
  local grace_elapsed=0

  "$@" &
  pid=$!

  while kill -0 "$pid" >/dev/null 2>&1; do
    if [[ -f "$PACKAGE_OUTPUT" ]] &&
       [[ -f "$log_file" ]] &&
       rg -q "Exported unitypackage to " "$log_file"; then
      if (( grace_elapsed >= grace_seconds )); then
        info "Unity export completed; terminating lingering batchmode process"
        kill "$pid" >/dev/null 2>&1 || true
        wait "$pid" >/dev/null 2>&1 || true
        return 0
      fi

      sleep 1
      grace_elapsed=$((grace_elapsed + 1))
      elapsed=$((elapsed + 1))
      continue
    fi

    if (( elapsed >= timeout_seconds )); then
      kill "$pid" >/dev/null 2>&1 || true
      wait "$pid" >/dev/null 2>&1 || true
      fail "Unity export timed out after ${timeout_seconds}s"
    fi

    sleep 1
    elapsed=$((elapsed + 1))
  done

  wait "$pid"
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "missing required command: $1"
}

find_unity_bin() {
  if [[ -n "${UNITY_BIN:-}" ]]; then
    [[ -x "$UNITY_BIN" ]] || fail "UNITY_BIN is not executable: $UNITY_BIN"
    printf '%s\n' "$UNITY_BIN"
    return
  fi

  if [[ "$(uname -s)" == "Darwin" ]]; then
    local candidate
    for candidate in /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity; do
      [[ -x "$candidate" ]] && { printf '%s\n' "$candidate"; return; }
    done
  fi
}

bump_versions() {
  info "Bumping Unity package version to $VERSION"
  run node - "$ROOT/package.json" "$VERSION" <<'NODE'
const fs = require("fs");
const path = process.argv[2];
const version = process.argv[3];
const text = fs.readFileSync(path, "utf8");
const updated = text.replace(/("version"\s*:\s*")[^"]+(")/, `$1${version}$2`);
if (updated === text && !text.includes(`"version": "${version}"`)) {
  throw new Error("Unable to update version in " + path);
}
fs.writeFileSync(path, updated);
NODE


}

run_git_checks() {
  info "Running git diff check"
  run git -C "$ROOT" diff --check
}

run_unity_tests() {
  [[ "$RUN_TESTS" == 1 ]] || { info "Skipping Unity tests"; return; }

  local unity_bin
  if [[ "$DRY_RUN" == 1 ]]; then
    unity_bin="${UNITY_BIN:-Unity}"
  else
    unity_bin="$(find_unity_bin)"
    [[ -n "$unity_bin" ]] || fail "Unity executable not found; set UNITY_BIN or pass --no-tests"
  fi

  info "Running Unity EditMode tests"
  if [[ "$DRY_RUN" == 1 ]]; then
    run "$unity_bin" \
      -batchmode \
      -projectPath "$PROJECT_ROOT" \
      -runTests \
      -testPlatform EditMode \
      -testResults "$OUT_DIR/editmode-results.xml" \
      -logFile "$OUT_DIR/unity-editmode-tests.log"
    return
  fi

  # Unity's own exit code reflects its NUnit "result" attribute, which goes
  # non-zero on an Ignored/Inconclusive test same as a real failure; this suite
  # always carries a few declared Ignores, so the gate has to be the "failed"
  # count in the results XML instead, same as CI's actual pass/fail policy.
  if ! "$unity_bin" \
    -batchmode \
    -projectPath "$PROJECT_ROOT" \
    -runTests \
    -testPlatform EditMode \
    -testResults "$OUT_DIR/editmode-results.xml" \
    -logFile "$OUT_DIR/unity-editmode-tests.log"; then
    :
  fi

  [[ -f "$OUT_DIR/editmode-results.xml" ]] || fail "Unity test results not found: $OUT_DIR/editmode-results.xml (see $OUT_DIR/unity-editmode-tests.log)"
  local failed
  failed="$(rg -o 'failed="[0-9]+"' "$OUT_DIR/editmode-results.xml" | head -1 | rg -o '[0-9]+')"
  [[ -n "$failed" ]] || fail "could not read failed-test count from $OUT_DIR/editmode-results.xml"
  [[ "$failed" == "0" ]] || fail "$failed Unity EditMode test(s) failed; see $OUT_DIR/unity-editmode-tests.log"
}

cleanup_exporter() {
  [[ "$DRY_RUN" == 0 ]] || return 0
  if [[ "${CREATED_EXPORTER_FILE:-0}" == 1 ]]; then
    rm -f "$EXPORTER_FILE" "$EXPORTER_FILE.meta"
    CREATED_EXPORTER_FILE=0
  fi
  if [[ "${CREATED_EDITOR_DIR:-0}" == 1 ]]; then
    rmdir "$PROJECT_ROOT/Assets/Editor" 2>/dev/null || true
  fi
}

export_unitypackage() {
  [[ "$EXPORT_PACKAGE" == 1 ]] || { info "Skipping unitypackage export"; return; }

  local unity_bin
  if [[ "$DRY_RUN" == 1 ]]; then
    unity_bin="${UNITY_BIN:-Unity}"
  else
    unity_bin="$(find_unity_bin)"
    [[ -n "$unity_bin" ]] || fail "Unity executable not found; set UNITY_BIN or pass --no-export"
  fi

  local editor_dir="$PROJECT_ROOT/Assets/Editor"
  CREATED_EDITOR_DIR=0
  if [[ ! -d "$editor_dir" ]]; then
    run mkdir -p "$editor_dir"
    CREATED_EDITOR_DIR=1
  fi

  EXPORTER_FILE="$editor_dir/ExportKitWrightUnityPackage.cs"
  trap cleanup_exporter EXIT

  local escaped_output
  escaped_output="$(printf '%s' "$PACKAGE_OUTPUT" | sed 's/\\/\\\\/g; s/"/\\"/g')"

  info "Creating temporary Unity export method"
  if [[ "$DRY_RUN" == 0 ]]; then
    [[ ! -e "$EXPORTER_FILE" && ! -e "$EXPORTER_FILE.meta" ]] || fail "Existing exporter preserved: $EXPORTER_FILE"
    CREATED_EXPORTER_FILE=1
    cat > "$EXPORTER_FILE" <<EOF
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExportKitWrightUnityPackage
{
    public static void Export()
    {
        const string assetPath = "Assets/unity-mcp";
        const string outputPath = "$escaped_output";

        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            Debug.LogError("Asset path not found: " + assetPath);
            EditorApplication.Exit(1);
            return;
        }

        var assetPaths = AssetDatabase.GetAllAssetPaths()
            .Where(IsAllowedPackagePath)
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (assetPaths.Length == 0)
        {
            Debug.LogError("No exportable assets found under " + assetPath);
            EditorApplication.Exit(1);
            return;
        }

        AssetDatabase.ExportPackage(assetPaths, outputPath, ExportPackageOptions.Default);
        Debug.Log("Exported unitypackage to " + outputPath);
    }

    private static bool IsAllowedPackagePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var normalized = path.Replace('\\\\', '/').TrimEnd('/');
        if (normalized != "Assets/unity-mcp" &&
            !normalized.StartsWith("Assets/unity-mcp/", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(normalized);
        if (string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, ".DS_Store.meta", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(fileName, "CLAUDE.md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "CLAUDE.md.meta", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(".mcpregistry_", StringComparison.OrdinalIgnoreCase))
            return false;

        if (ContainsPathSegment(normalized, ".git") ||
            ContainsPathSegment(normalized, ".idea") ||
            ContainsPathSegment(normalized, ".claude") ||
            ContainsPathSegment(normalized, ".codex"))
            return false;

        if (ContainsPathSegment(normalized, ".github"))
            return false;

        if (ContainsPathSegment(normalized, "Tests"))
            return false;

        if (ContainsPathSegment(normalized, "scripts"))
            return false;

        return !ContainsPathSegment(normalized, "ProjectSettings") &&
               !ContainsPathSegment(normalized, "Packages") &&
               !ContainsPathSegment(normalized, "Library");
    }

    private static bool ContainsPathSegment(string path, string segment)
    {
        return path.Split('/').Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }
}
EOF
  fi

  info "Exporting unitypackage"
  run_unity_export "$unity_bin" \
    -batchmode -quit \
    -projectPath "$PROJECT_ROOT" \
    -executeMethod ExportKitWrightUnityPackage.Export \
    -logFile "$OUT_DIR/unitypackage-export.log"
  cleanup_exporter
}

validate_unitypackage() {
  [[ -f "$PACKAGE_OUTPUT" ]] || fail "unitypackage not found: $PACKAGE_OUTPUT"
  require_command tar

  info "Validating unitypackage pathnames"
  VALIDATION_TMP="$(mktemp -d)"
  trap '[[ -z "${VALIDATION_TMP:-}" ]] || rm -rf "$VALIDATION_TMP"; cleanup_exporter' EXIT

  # -f with a drive-letter path (C:/...) reads as tar's user@host:path remote
  # syntax on some tar builds (MSYS/Git Bash); stdin redirection sidesteps it.
  tar -xz -C "$VALIDATION_TMP" < "$PACKAGE_OUTPUT"
  find "$VALIDATION_TMP" -name pathname -print0 \
    | while IFS= read -r -d '' file; do tr -d '\000' < "$file"; printf '\n'; done \
    | sort > "$OUT_DIR/unitypackage-pathnames.txt"

  local bad_paths
  bad_paths="$(awk '$0 !~ /^Assets\/unity-mcp(\/|$)/ {print}' "$OUT_DIR/unitypackage-pathnames.txt" || true)"
  [[ -z "$bad_paths" ]] || fail "unitypackage contains paths outside Assets/unity-mcp:\n$bad_paths"

  local blocked_paths
  blocked_paths="$(rg '(^|/)(ProjectSettings|Packages|Library|Tests|scripts|\.git|\.idea|\.claude|\.codex|\.github)(/|$)|(^|/)(CLAUDE\.md|\.DS_Store)(\.meta)?$|(^|/)\.mcpregistry_' "$OUT_DIR/unitypackage-pathnames.txt" || true)"
  [[ -z "$blocked_paths" ]] || fail "unitypackage contains blocked paths:\n$blocked_paths"
}

generate_notes_and_sums() {
  info "Generating release notes and SHA256SUMS"
  {
    if awk -v version="$VERSION" '
      $0 ~ "^## \\[" version "\\]" { found = 1; next }
      found && /^## \[/ { exit }
      found { print }
      END { exit found ? 0 : 1 }
    ' "$ROOT/CHANGELOG.md"; then
      true
    else
      echo "Release v$VERSION"
      echo
      git -C "$ROOT" log --oneline -10
    fi
  } > "$NOTES_FILE"

  (
    cd "$OUT_DIR"
    : > "$SHA_FILE"
    for file in KitWright.UnityMcp.v"$VERSION".unitypackage *.nupkg server.json; do
      [[ -f "$file" ]] || continue
      shasum -a 256 "$file" >> "$SHA_FILE"
    done
  )

  cat > "$MANIFEST_FILE" <<EOF
{
  "version": "$VERSION",
  "gitSha": "$(git -C "$ROOT" rev-parse HEAD)",
  "unityProjectRoot": "$PROJECT_ROOT",
  "unityPackage": "$PACKAGE_OUTPUT",
  "generatedAtUtc": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
}
EOF
}

# Refuse before any source/artifact mutation. Publishing never moves a branch,
# overwrites a tag/release or packages unreviewed working-tree contents.
check_publication_source() {
  [[ "$PUBLISH_GITHUB" == 1 && "$DRY_RUN" == 0 ]] || return 0
  require_command git
  require_command gh
  local origin remote_main version
  origin="$(git -C "$ROOT" remote get-url origin)"
  case "$origin" in
    https://github.com/amynoox/unity-mcp|https://github.com/amynoox/unity-mcp.git|git@github.com:amynoox/unity-mcp.git) ;;
    *) fail "Publication requires the amynoox/unity-mcp origin" ;;
  esac
  [[ -z "$(git -C "$ROOT" status --porcelain --untracked-files=no)" ]] || fail "Commit/review source changes before publishing"
  RELEASE_COMMIT="$(git -C "$ROOT" rev-parse HEAD)"
  remote_main="$(git -C "$ROOT" ls-remote --exit-code origin refs/heads/main | cut -f1)"
  [[ "$RELEASE_COMMIT" == "$remote_main" ]] || fail "Publication requires the exact reviewed remote main commit"
  version="$(node -p 'JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")).version' "$ROOT/package.json")"
  [[ "$version" == "$VERSION" ]] || fail "Requested version must already be committed and merged into main"
  [[ -z "$(git -C "$ROOT" ls-remote --tags origin "refs/tags/v$VERSION" "refs/tags/v$VERSION^{}")" ]] || fail "Existing release tag preserved"
  if gh release view "v$VERSION" -R "$REPO" >/dev/null 2>&1; then
    fail "Existing release preserved; inspect it instead of replaying publication"
  fi
}

publish_github() {
  [[ "$PUBLISH_GITHUB" == 1 ]] || return 0
  info "Creating a Draft GitHub Release for $REPO"
  run gh release create "v$VERSION" "$PACKAGE_OUTPUT" -R "$REPO" \
    --draft --title "v$VERSION" --notes-file "$NOTES_FILE" \
    --target "${RELEASE_COMMIT:-<reviewed-main-commit>}"
}

archive_artifacts() {
  info "Release artifacts"
  find "$OUT_DIR" -maxdepth 1 -type f -print | sort
}

main() {
  require_command node
  require_command rg
  require_command shasum
  check_publication_source
  if [[ "$DRY_RUN" == 0 && ( "$RUN_TESTS" == 1 || "$EXPORT_PACKAGE" == 1 ) ]]; then
    [[ -n "$PROJECT_ROOT" && -d "$PROJECT_ROOT/Assets" ]] || fail "Set UNITY_PROJECT_ROOT to the explicitly authorized idle test project"
  fi
  run mkdir -p "$OUT_DIR"

  bump_versions
  run_git_checks
  run_unity_tests
  export_unitypackage
  if [[ "$DRY_RUN" == 1 ]]; then
    publish_github
    info "Dry run complete; no files, Unity process or publication changed"
    return
  fi
  validate_unitypackage
  generate_notes_and_sums
  publish_github
  archive_artifacts
}

main "$@"
