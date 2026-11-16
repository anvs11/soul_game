#!/usr/bin/env python3
"""Sequential Unity validation; explicit editor path, no install or lock removal."""
import argparse, datetime, json, pathlib, subprocess, uuid, xml.etree.ElementTree as ET
parser = argparse.ArgumentParser()
parser.add_argument('--editor', required=True)
parser.add_argument('--build', choices=['Mac', 'Windows', 'Android'])
args = parser.parse_args()
root = pathlib.Path(__file__).resolve().parents[2]
project = root / 'Game'
lock = project / 'Temp/UnityLockfile'
if lock.exists():
    check = subprocess.run(['lsof', '-t', str(lock)], capture_output=True, text=True)
    if check.stdout.strip(): raise SystemExit('Unity project is open; close Editor first. Lock was preserved.')
editor = pathlib.Path(args.editor)
if not editor.is_file(): raise SystemExit('Explicit Unity executable not found.')
run = project / 'TestResults' / ('stage2-validation-' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
run.mkdir(parents=True, exist_ok=False)
summary = {'editor': str(editor), 'project': str(project), 'results': []}
for mode in ['EditMode', 'PlayMode']:
    xml = run / (mode + '.xml')
    command = [str(editor), '-batchmode', '-projectPath', str(project), '-runTests', '-testPlatform', mode,
               '-testResults', str(xml), '-logFile', str(run / (mode + '.log'))]
    completed = subprocess.run(command, cwd=root)
    item = {'platform': mode, 'exit': completed.returncode, 'xml': str(xml)}
    if xml.exists():
        data = ET.parse(xml).getroot(); cases = list(data.iter('test-case'))
        item.update(total=len(cases), passed=sum(x.get('result') == 'Passed' for x in cases),
                    failed=sum(x.get('result') == 'Failed' for x in cases),
                    nonpassed=[x.get('fullname') for x in cases if x.get('result') != 'Passed'], result=data.get('result'))
        item['ok'] = completed.returncode == 0 and bool(cases) and not item['nonpassed'] and data.get('result') == 'Passed'
    else: item['ok'] = False
    summary['results'].append(item)
    (run / 'validation-summary.json').write_text(json.dumps(summary, indent=2))
    print(json.dumps(item), flush=True)
    if not item['ok']: raise SystemExit('Test failure; evidence preserved in ' + str(run))
if args.build:
    target = {'Mac': 'StandaloneOSX', 'Windows': 'StandaloneWindows64', 'Android': 'Android'}[args.build]
    before = set((project / 'Builds/Stage2' / args.build).glob('*/build-report.json'))
    command = [str(editor), '-batchmode', '-quit', '-projectPath', str(project), '-buildTarget', target,
               '-executeMethod', 'Game.Editor.BuildCommands.BuildStage2' + args.build,
               '-logFile', str(run / ('build-' + args.build + '.log'))]
    completed = subprocess.run(command, cwd=root)
    after = set((project / 'Builds/Stage2' / args.build).glob('*/build-report.json'))
    reports = after - before
    item = {'platform': args.build, 'exit': completed.returncode, 'reports': [str(p) for p in reports]}
    item['ok'] = completed.returncode == 0 and len(reports) == 1
    if reports:
        report = json.loads(next(iter(reports)).read_text()); item['buildReport'] = report
        item['ok'] = item['ok'] and report.get('result') == 'Succeeded' and report.get('errors') == 0
    summary['build'] = item
    (run / 'validation-summary.json').write_text(json.dumps(summary, indent=2))
    print(json.dumps(item), flush=True)
    if not item['ok']: raise SystemExit('Build not verified; evidence preserved in ' + str(run))
print('VERIFIED ' + str(run), flush=True)
