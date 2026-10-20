"""Validate Stryker's real JSON report, preserve its statuses, and compare runs."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

VALID = {'Killed', 'Survived', 'Timeout', 'NoCoverage', 'CompileError', 'Ignored'}

def load_run(directory):
    root = Path(directory)
    run = json.loads((root / 'run.json').read_text(encoding='utf-8-sig'))
    trx_files=list((root/'preflight').glob('*.trx'))
    if len(trx_files)!=1:
        raise ValueError('Expected exactly one unmutated preflight TRX')
    counters=ET.parse(trx_files[0]).find('.//{*}Counters')
    if counters is None or int(counters.get('total','0')) == 0 or int(counters.get('passed','0')) != int(counters.get('total','0')):
        raise ValueError('Preflight is empty, failed, or skipped; cannot accept mutation evidence')
    reports = list((root / 'stryker').rglob('mutation-report.json'))
    if len(reports) != 1:
        raise ValueError(f'Expected exactly one Stryker JSON report, found {len(reports)}')
    report = json.loads(reports[0].read_text(encoding='utf-8-sig'))
    counts = Counter()
    mutants = []
    seen = set()
    for name, file in report['files'].items():
        for mutant in file['mutants']:
            status = mutant['status']
            if status not in VALID:
                raise ValueError(f'Unknown or incomplete mutant status: {status}')
            key = (name, mutant['id'])
            if key in seen:
                raise ValueError(f'Duplicate mutant identity: {key}')
            seen.add(key)
            counts[status] += 1
            mutants.append(dict(file=name, **mutant))
    valid = sum(counts[x] for x in ('Killed', 'Timeout', 'Survived', 'NoCoverage'))
    if not valid:
        raise ValueError('No valid tested/uncovered mutants; no score can be claimed')
    score = 100 * (counts['Killed'] + counts['Timeout']) / valid
    result = {
        'run': run, 'report': str(reports[0].relative_to(root)),
        'counts': {s: counts[s] for s in sorted(VALID)},
        'validMutants': valid, 'mutationScore': score,
        'formula': '100 * (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)',
        'mutants': mutants,
    }
    return root, result

def summarise(directory):
    root, result = load_run(directory)
    (root / 'summary.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    lines = ['# Mutation execution summary', '',
             f"Stryker exit code: {result['run']['exitCode']}",
             f"Mutation score: {result['mutationScore']:.2f}%", '', '| Status | Count |', '| --- | ---: |']
    lines += [f'| {s} | {v} |' for s, v in result['counts'].items()]
    lines += ['', result['formula'], '', '## Surviving and uncovered mutants', '']
    for m in result['mutants']:
        if m['status'] in ('Survived', 'NoCoverage'):
            pos = m['location']['start']
            lines.append(f"- {m['status']} {m['file']}:{pos['line']}:{pos['column']} [{m['id']}] {m.get('mutatorName', m.get('mutator', ''))}: {m.get('replacement', '')!r}")
    (root / 'summary.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    entries = []
    for path in sorted(root.rglob('*')):
        if path.is_file() and path.name != 'SHA256SUMS.txt':
            entries.append(f'{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.relative_to(root).as_posix()}')
    (root / 'SHA256SUMS.txt').write_text('\n'.join(entries)+'\n', encoding='utf-8')
    print(f"Mutation score {result['mutationScore']:.2f}%; statuses {result['counts']}")
    if result['run']['exitCode'] != 0:
        raise ValueError('Stryker execution failed or threshold was not met; evidence retained, not accepted as a passing run')

def compare(before, after, output):
    _, b = load_run(before)
    _, a = load_run(after)
    def index(result):
        return {(m['file'], json.dumps(m['location'], sort_keys=True), m.get('mutatorName', m.get('mutator')), m.get('replacement')): m for m in result['mutants']}
    bi, ai = index(b), index(a)
    if bi.keys() != ai.keys():
        raise ValueError('Mutation sets differ; a direct before/after comparison is not valid')
    for filename in ('stryker-config.json', 'dotnet-tools.json'):
        if json.loads((Path(before)/filename).read_text(encoding='utf-8-sig')) != json.loads((Path(after)/filename).read_text(encoding='utf-8-sig')):
            raise ValueError(f'{filename} changed; compare runs with the same scope and tool version')
    changes = [{'file': key[0], 'location': bm['location'], 'mutator': bm.get('mutatorName', bm.get('mutator')), 'replacement': bm.get('replacement'),
                'before': bm['status'], 'after': ai[key]['status']} for key, bm in bi.items() if bm['status'] != ai[key]['status']]
    result = {'beforeScore': b['mutationScore'], 'afterScore': a['mutationScore'], 'beforeCounts': b['counts'], 'afterCounts': a['counts'], 'changes': changes}
    Path(output).write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--results')
    p.add_argument('--before')
    p.add_argument('--after')
    p.add_argument('--output')
    args = p.parse_args()
    try:
        if args.results:
            summarise(args.results)
        elif args.before and args.after and args.output:
            compare(args.before, args.after, args.output)
        else:
            p.error('Use --results, or --before/--after/--output')
    except (ValueError, KeyError, OSError, TypeError) as exc:
        print(f'Evidence rejected: {exc}', file=sys.stderr)
        return 1
    return 0

if __name__ == '__main__':
    sys.exit(main())
