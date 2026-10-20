"""Evidence guards: incomplete or incomparable mutation runs must fail visibly."""
import json
from pathlib import Path
import tempfile
import unittest
from summarise_mutation import load_run, summarise, compare

class MutationEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)

    def fixture(self, name='run', statuses=('Killed','Timeout','Survived','NoCoverage','CompileError','Ignored')):
        root=self.root/name
        (root/'stryker'/'reports').mkdir(parents=True)
        (root/'preflight').mkdir()
        (root/'preflight'/'preflight.trx').write_text('<TestRun><ResultSummary><Counters total="2" passed="2"/></ResultSummary></TestRun>')
        (root/'run.json').write_text(json.dumps({'exitCode':0}))
        (root/'stryker-config.json').write_text('{}')
        (root/'dotnet-tools.json').write_text('{}')
        mutants=[{'id':str(i),'status':s,'mutator':'Boolean','replacement':'false','location':{'start':{'line':i+1,'column':1},'end':{'line':i+1,'column':2}}} for i,s in enumerate(statuses)]
        (root/'stryker'/'reports'/'mutation-report.json').write_text(json.dumps({'files':{'Services/Test.cs':{'mutants':mutants}}}))
        return root

    def test_score_excludes_ignored_and_compile_errors_but_keeps_uncovered(self):
        _,r=load_run(self.fixture())
        self.assertEqual(r['mutationScore'],50)
        self.assertEqual(r['validMutants'],4)

    def test_empty_and_pending_reports_are_rejected(self):
        for i,statuses in enumerate([(),('Ignored','CompileError'),('Pending',)]):
            with self.subTest(statuses=statuses), self.assertRaises(ValueError):
                load_run(self.fixture(str(i),statuses))

    def test_duplicate_reports_are_rejected(self):
        root=self.fixture()
        source=root/'stryker'/'reports'/'mutation-report.json'
        (root/'stryker'/'mutation-report.json').write_bytes(source.read_bytes())
        with self.assertRaises(ValueError): load_run(root)

    def test_nonzero_execution_is_not_accepted_as_passing(self):
        root=self.fixture()
        (root/'run.json').write_text('{"exitCode":1}')
        with self.assertRaises(ValueError): summarise(root)
        self.assertTrue((root/'summary.json').exists())

    def test_empty_or_partially_passing_preflight_is_rejected(self):
        for i,(total,passed) in enumerate([(0,0),(2,1)]):
            root=self.fixture(str(i))
            (root/'preflight'/'preflight.trx').write_text(f'<TestRun><ResultSummary><Counters total="{total}" passed="{passed}"/></ResultSummary></TestRun>')
            with self.assertRaises(ValueError): load_run(root)

    def test_changed_scope_and_changed_tool_version_reject_comparison(self):
        b=self.fixture('before')
        a=self.fixture('after')
        (a/'dotnet-tools.json').write_text('{"version":"different"}')
        with self.assertRaises(ValueError): compare(b,a,self.root/'comparison.json')

    def test_matched_mutant_status_transition_is_recorded(self):
        b=self.fixture('before',('Survived',))
        a=self.fixture('after',('Killed',))
        target=self.root/'comparison.json'
        compare(b,a,target)
        r=json.loads(target.read_text())
        self.assertEqual((r['beforeScore'],r['afterScore']),(0,100))
        self.assertEqual(r['changes'][0]['after'],'Killed')

if __name__=='__main__': unittest.main()
