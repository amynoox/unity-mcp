"""Exercise the result reporter without launching Unity or masking omitted native gates."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET


REPORTER = Path(__file__).resolve().parents[1] / "report_test_skips.py"
FOCUS = "KitWright.Editor.Tests.EditorFocusFunctionsTests.NativeFocusCommandsVerifyActualForegroundAndGameView"
EXPLICIT_REASON = "Owned interactive disposable project only; owner must select another app during the initial delay."


class ReportTestSkipsTests(unittest.TestCase):
    def report(self, *, name=FOCUS, result="Skipped", reason=EXPLICIT_REASON, **counts):
        attrs = dict(total="1", passed="0", failed="0", skipped="1", inconclusive="0")
        attrs.update({k: str(v) for k, v in counts.items()})
        root = ET.Element("test-run", attrs)
        case = ET.SubElement(root, "test-case", fullname=name, result=result)
        if reason is not None:
            ET.SubElement(ET.SubElement(case, "reason"), "message").text = reason
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            ET.ElementTree(root).write(path, encoding="utf-8")
            env = dict(os.environ)
            env.pop("GITHUB_STEP_SUMMARY", None)
            return subprocess.run([sys.executable, str(REPORTER), "LAN fixture", str(path)],
                                  capture_output=True, text=True, encoding="utf-8", env=env)

    def test_exact_explicit_focus_omission_remains_a_deferred_gate(self):
        report = self.report()
        self.assertEqual(0, report.returncode, report.stdout + report.stderr)
        self.assertIn("0 passed", report.stdout)
        self.assertIn("Interactive focus qualification is still pending", report.stdout)
        self.assertIn("not passes", report.stdout)

    def test_same_reason_on_another_test_is_not_allowed(self):
        report = self.report(name="Other.Test")
        self.assertEqual(1, report.returncode)
        self.assertIn("not in", report.stdout)

    def test_current_explicit_reason_is_still_deferred_not_a_pass(self):
        reason = "Owned interactive disposable project only; test temporarily backgrounds this Editor."
        report = self.report(reason=reason)
        self.assertEqual(0, report.returncode, report.stdout + report.stderr)
        self.assertIn("0 passed", report.stdout)
        self.assertIn("Interactive focus qualification is still pending", report.stdout)
        self.assertEqual(1, self.report(name="Other.Test", reason=reason).returncode)

    def test_changed_focus_reason_is_not_allowed(self):
        self.assertEqual(1, self.report(reason="Unexpected interruption").returncode)

    def test_explicit_reason_does_not_hide_an_inconclusive_focus_test(self):
        self.assertEqual(1, self.report(result="Inconclusive", skipped=0, inconclusive=1).returncode)

    def test_missing_reason_is_not_allowed(self):
        self.assertEqual(1, self.report(reason=None).returncode)

    def test_inconclusive_is_separate_from_skipped_and_passed(self):
        report = self.report(name="ReferenceStress", result="Inconclusive",
                             reason="Too few assemblies loaded", skipped=0, inconclusive=1)
        self.assertEqual(0, report.returncode, report.stdout + report.stderr)
        self.assertIn("0 skipped, 1 inconclusive", report.stdout)
        self.assertIn("| Inconclusive |", report.stdout)

    def test_failure_always_fails_reporter(self):
        self.assertEqual(1, self.report(result="Failed", skipped=0, failed=1).returncode)

    def test_empty_run_fails_reporter(self):
        report = self.report(total=0, skipped=0)
        self.assertEqual(1, report.returncode)
        self.assertIn("No test results", report.stdout)


if __name__ == "__main__":
    unittest.main()
