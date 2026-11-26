"""Guard evidence acceptance against failed, incomplete or duplicated test output."""
import json
import tempfile
import unittest
from pathlib import Path

from summarise_unit import summarise


class EvidenceAcceptanceTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.projects = ['FleetFlow.IdentityService.Tests', 'FleetFlow.VehicleService.Tests']
        self.run = {'exitCode': 0, 'sourceCommit': 'synthetic-fixture', 'sourceHadUncommittedChanges': False}
        self.write_run()
        for project in self.projects:
            folder = self.root / project
            folder.mkdir()
            (folder / 'results.trx').write_text(
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                '<Results><UnitTestResult testName="synthetic" outcome="Passed" /></Results></TestRun>')
            # Identical locations exercise union, not addition or averaged percentages.
            coverage = {'Service.dll': {'/src/Service.cs': {'Service': {'Method': {
                'Lines': {'10': 1, '11': 0}, 'Branches': [
                    {'Line': 10, 'Offset': 0, 'EndOffset': 5, 'Path': 0, 'Hits': 1},
                    {'Line': 10, 'Offset': 0, 'EndOffset': 8, 'Path': 1, 'Hits': 0}]}}}}}
            (folder / 'coverage.json').write_text(json.dumps(coverage))

    def write_run(self):
        (self.root / 'run.json').write_text(json.dumps(self.run))

    def test_complete_passing_results_are_accepted_with_union_coverage(self):
        result = summarise(self.root)
        self.assertTrue(result['accepted'])
        self.assertEqual(2, result['passed'])
        self.assertEqual({'covered': 1, 'total': 2, 'percent': 50.0}, result['lineCoverage'])
        self.assertEqual(2, result['branchCoverage']['total'])
        self.assertTrue((self.root / 'SHA256SUMS.txt').is_file())

    def test_nonzero_process_exit_is_rejected_even_when_trx_passes(self):
        self.run['exitCode'] = 1
        self.write_run()
        self.assertFalse(summarise(self.root)['accepted'])

    def test_failed_or_skipped_cases_are_rejected(self):
        path = self.root / self.projects[0] / 'results.trx'
        original = path.read_text()
        for outcome in ['Failed', 'NotExecuted']:
            with self.subTest(outcome=outcome):
                path.write_text(original.replace('Passed', outcome))
                self.assertFalse(summarise(self.root)['accepted'])

    def test_missing_project_result_is_rejected(self):
        (self.root / self.projects[1] / 'results.trx').unlink()
        self.assertFalse(summarise(self.root)['accepted'])

    def test_missing_project_coverage_is_rejected(self):
        (self.root / self.projects[1] / 'coverage.json').unlink()
        self.assertFalse(summarise(self.root)['accepted'])

    def test_duplicate_results_cannot_inflate_a_passing_run(self):
        folder = self.root / self.projects[0]
        (folder / 'duplicate.trx').write_bytes((folder / 'results.trx').read_bytes())
        self.assertFalse(summarise(self.root)['accepted'])

    def test_vstest_attachment_copy_is_accepted_without_double_counting(self):
        folder = self.root / self.projects[0]
        copy = folder / 'In' / 'machine'
        copy.mkdir(parents=True)
        (copy / 'coverage.json').write_bytes((folder / 'coverage.json').read_bytes())
        result = summarise(self.root)
        self.assertTrue(result['accepted'])
        self.assertEqual(2, result['lineCoverage']['total'])

    def test_conflicting_coverage_in_one_project_is_rejected(self):
        folder = self.root / self.projects[0]
        extra = folder / 'other'
        extra.mkdir()
        (extra / 'coverage.json').write_text('{}')
        self.assertFalse(summarise(self.root)['accepted'])

    def test_zero_tests_are_rejected(self):
        for project in self.projects:
            path = self.root / project / 'results.trx'
            path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" />')
        self.assertFalse(summarise(self.root)['accepted'])


if __name__ == '__main__':
    unittest.main()
