"""Keep hosted validation opt-in; this test never invokes GitHub."""
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]


class ValidationWorkflowPolicyTests(unittest.TestCase):
    def test_ci_and_codeql_are_manual_only(self):
        for name in ("ci.yml", "codeql.yml"):
            with self.subTest(workflow=name):
                text = (ROOT / ".github" / "workflows" / name).read_text()
                triggers = text.split("jobs:", 1)[0]
                self.assertIn("workflow_dispatch:", triggers)
                for automatic in ("    push:", "    pull_request:", "    schedule:", "    pull_request_target:"):
                    self.assertNotIn(automatic, triggers)


if __name__ == "__main__":
    unittest.main()
