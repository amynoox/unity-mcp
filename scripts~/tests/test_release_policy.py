"""Exercise release refusal/dry-run paths without Unity, GitHub or publishing."""
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts~' / 'release.sh'


def shell_path(path):
    value = str(path).replace('\\', '/')
    if os.name == 'nt' and len(value) > 1 and value[1] == ':':
        return '/' + value[0].lower() + value[2:]
    return value


class ReleasePolicyTests(unittest.TestCase):
    def setUp(self):
        if os.name == 'nt':
            git = shutil.which('git')
            bash = Path(git).parent.parent / 'bin/bash.exe' if git else None
            self.bash = str(bash) if bash and bash.is_file() else None
        else:
            self.bash = shutil.which('bash')
        if not self.bash:
            self.skipTest('Native Bash/Git Bash is required; never use WSL.')
        self.temp = tempfile.TemporaryDirectory(prefix='fork-release-policy-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bin = self.root / 'bin'
        self.bin.mkdir()
        self.log = self.root / 'calls'
        for command in ('node', 'rg', 'shasum', 'gh'):
            file = self.bin / command
            file.write_text('#!/usr/bin/env bash\nprintf "%s\\n" "$0 $*" >> "$CALL_LOG"\nexit 95\n')
            file.chmod(0o755)
        git = self.bin / 'git'
        git.write_text('#!/usr/bin/env bash\nprintf "%s\\n" "$*" >> "$CALL_LOG"\n'
                       'case "$*" in\n*"remote get-url origin"*) echo "$FIXTURE_ORIGIN";;\n'
                       '*) exit 96;;\nesac\n')
        git.chmod(0o755)
        self.env = dict(os.environ, TEST_BIN=shell_path(self.bin), CALL_LOG=shell_path(self.log),
                        FIXTURE_ORIGIN='https://github.com/kitwright/unity-mcp.git',
                        RELEASE_ARTIFACT_DIR=shell_path(self.root / 'artifacts'))
        self.before = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                       for name in ('package.json', 'README.md', 'README_CN.md')}

    def run_script(self, *args):
        result = subprocess.run([self.bash, '-c', 'export PATH="$TEST_BIN:$PATH"; exec bash "$@"',
                                 'release-fixture', shell_path(SCRIPT), '1.2.3', *args],
                                env=self.env, cwd=ROOT, text=True, capture_output=True, timeout=30)
        self.assertEqual(self.before, {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                                      for name in self.before})
        self.assertFalse((self.root / 'artifacts').exists())
        return result

    def test_dry_run_only_previews_a_draft_release_for_this_fork(self):
        result = self.run_script('--dry-run', '--no-tests', '--no-export', '--publish-github')
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn('-R amynoox/unity-mcp', result.stdout)
        self.assertIn('--draft', result.stdout)
        self.assertFalse(self.log.exists(), 'Dry run invoked an external action.')

    def test_upstream_origin_is_refused_before_mutation(self):
        result = self.run_script('--no-tests', '--no-export', '--publish-github')
        self.assertNotEqual(0, result.returncode)
        self.assertIn('requires the amynoox/unity-mcp origin', result.stderr)
        self.assertIn('remote get-url origin', self.log.read_text())
        self.assertNotIn('release create', self.log.read_text())

    def test_unconfigured_channels_are_refused_before_any_action(self):
        for flag in ('--publish-nuget', '--publish-registry', '--verify-openupm'):
            with self.subTest(flag=flag):
                result = self.run_script(flag)
                self.assertNotEqual(0, result.returncode)
                self.assertIn('not configured for this fork', result.stderr)
                self.assertFalse(self.log.exists())


if __name__ == '__main__':
    unittest.main()
