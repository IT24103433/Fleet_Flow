"""Build a dependency-free HTML/JSON report from actual TRX and Coverlet JSON.

Coverage is the union of production source locations, not the average of percentages.
Missing reports, zero tests, failures, skipped tests and failed execution all fail the command.
"""
import argparse
import hashlib
import html
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def summarise(results: Path) -> dict:
    namespace = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = []
    trx_paths = sorted(results.rglob('*.trx'))
    expected_projects = {'FleetFlow.IdentityService.Tests', 'FleetFlow.VehicleService.Tests'}
    projects_with_results = {p.relative_to(results).parts[0] for p in trx_paths}
    for path in sorted(results.rglob('*.trx')):
        root = ET.parse(path)
        for item in root.findall('.//t:UnitTestResult', namespace):
            cases.append({'name': item.get('testName'), 'outcome': item.get('outcome'), 'duration': item.get('duration')})
    lines, branches, types = {}, {}, {}
    coverage_paths = sorted(results.rglob('coverage.json'))
    projects_with_coverage = {p.relative_to(results).parts[0] for p in coverage_paths}
    # VSTest may copy collector attachments into an additional In/<machine> folder.
    # Identical copies in one project are harmless; conflicting reports are rejected.
    coverage_versions = {}
    for path in coverage_paths:
        project = path.relative_to(results).parts[0]
        coverage_versions.setdefault(project, set()).add(hashlib.sha256(path.read_bytes()).hexdigest())
    for path in coverage_paths:
        for assembly, documents in json.loads(path.read_text(encoding='utf-8-sig')).items():
            for document, classes in documents.items():
                for name, methods in classes.items():
                    # Compiler-generated async state machines belong to their declaring type.
                    type_name = name.split('/')[0]
                    type_lines, type_branches = types.setdefault(type_name, ({}, {}))
                    for method, detail in methods.items():
                        for number, hits in detail.get('Lines', {}).items():
                            key = (assembly, document, int(number))
                            lines[key] = lines.get(key, False) or hits > 0
                            type_lines[key] = type_lines.get(key, False) or hits > 0
                        for branch in detail.get('Branches', []):
                            key = (assembly, document, method, branch['Line'], branch['Offset'], branch['EndOffset'], branch['Path'])
                            branches[key] = branches.get(key, False) or branch['Hits'] > 0
                            type_branches[key] = type_branches.get(key, False) or branch['Hits'] > 0
    def metric(values):
        return {'covered': sum(values), 'total': len(values), 'percent': round(sum(values) * 100 / len(values), 2) if values else None}
    run = json.loads((results / 'run.json').read_text(encoding='utf-8-sig'))
    summary = {'run': run, 'tests': len(cases), 'passed': sum(c['outcome'] == 'Passed' for c in cases),
               'nonPassing': sum(c['outcome'] != 'Passed' for c in cases), 'lineCoverage': metric(list(lines.values())),
               'branchCoverage': metric(list(branches.values())), 'coverageScope': 'Five selected production types; full types include untested methods. Not whole-system coverage.',
               'coverageByType': {name: {'lines': metric(list(values[0].values())), 'branches': metric(list(values[1].values()))}
                                  for name, values in sorted(types.items())}, 'cases': cases}
    summary['projectsWithResults'] = sorted(projects_with_results)
    summary['projectsWithCoverage'] = sorted(projects_with_coverage)
    summary['accepted'] = (projects_with_results == expected_projects and projects_with_coverage == expected_projects
                           and len(trx_paths) == 2 and all(len(v) == 1 for v in coverage_versions.values()) and run['exitCode'] == 0
                           and len(cases) > 0 and summary['nonPassing'] == 0 and bool(lines) and bool(branches))
    (results / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    rows = ''.join('<tr><td>' + html.escape(c['name'] or '') + '</td><td>' + html.escape(c['outcome'] or '') + '</td></tr>' for c in cases)
    coverage_rows = ''.join(f'<tr><td>{html.escape(name)}</td><td>{value["lines"]}</td><td>{value["branches"]}</td></tr>'
                            for name, value in summary['coverageByType'].items())
    raw_files = [p for p in sorted(results.rglob('*')) if p.is_file() and
                 (p.suffix in {'.trx', '.xml', '.log'} or p.name in {'coverage.json', 'run.json'})]
    links = ''.join(f'<li><a href="{html.escape(p.relative_to(results).as_posix(), quote=True)}">'
                    f'{html.escape(p.relative_to(results).as_posix())}</a></li>' for p in raw_files)
    document = f'''<!doctype html><html lang="en"><meta charset="utf-8"><title>FleetFlow ASE unit test evidence</title>
<style>body{{font:16px Arial,sans-serif;color:#172131;max-width:1100px;margin:40px auto;padding:0 24px}}h1,h2{{color:#111}}table{{border-collapse:collapse;width:100%}}th,td{{padding:9px;border:1px solid #d9d9d9;text-align:left}}th{{background:#061d53;color:white}}tr:nth-child(even){{background:#f5f6f8}}code{{overflow-wrap:anywhere}}</style>
<h1>FleetFlow Automations ASE unit testing</h1><p>Sanjeevsaran J. | IT24104155 | Group 27</p>
<p>Source <code>{html.escape(run['sourceCommit'])}</code>; uncommitted changes at execution: {run['sourceHadUncommittedChanges']}.</p>
<p>Executed {summary['tests']} tests: {summary['passed']} passed; {summary['nonPassing']} not passing. Exit code: {run['exitCode']}.</p>
<h2>Measured coverage</h2><p>Lines: {summary['lineCoverage']}; branches: {summary['branchCoverage']}.</p>
<p>{html.escape(summary['coverageScope'])} No database, HTTP server or broker is required. Docker mode disables networking.</p>
<table><tr><th>Production type</th><th>Lines</th><th>Branches</th></tr>{coverage_rows}</table>
<h2>Raw execution evidence</h2><ul>{links}</ul>
<h2>Actual case outcomes</h2><table><tr><th>Test</th><th>Outcome</th></tr>{rows}</table></html>'''
    (results / 'index.html').write_text(document, encoding='utf-8')
    entries = []
    for path in sorted(results.rglob('*')):
        if path.is_file() and path.name != 'SHA256SUMS.txt':
            entries.append(hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + path.relative_to(results).as_posix())
    (results / 'SHA256SUMS.txt').write_text('\n'.join(entries) + '\n', encoding='utf-8')
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--results', type=Path, required=True)
    result = summarise(parser.parse_args().results.resolve())
    print(json.dumps({k: result[k] for k in ['tests', 'passed', 'nonPassing', 'lineCoverage', 'branchCoverage', 'accepted']}, indent=2))
    raise SystemExit(0 if result['accepted'] else 1)
