#!/usr/bin/env python3
"""Independent reference model, NOT a C# runner or physical Unity validation."""
import argparse
import collections
import copy
import hashlib
import json
import pathlib
import platform
import sys
import time
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
MASK = 0xffffffff


def checked_path(value):
    path = pathlib.Path(value).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError('Path must be inside the project')
    return path


def catalog_data(data):
    if any(type(data[k]) is not int or data[k] != 1 for k in ('schemaVersion', 'generatorVersion', 'contentVersion')):
        raise ValueError('Only schema/generator/content versions 1/1/1 are supported')
    modules = {}
    for m in data['modules']:
        name = m['id']
        if not isinstance(name, str) or not name.strip() or name in modules:
            raise ValueError('Empty or duplicate Id: ' + str(name))
        if m['kind'] not in ('Entry', 'Combat', 'Rest', 'Boss'):
            raise ValueError('Unknown kind: ' + name)
        w, h = m['width'], m['height']
        if type(w) is not int or type(h) is not int or w <= 0 or h <= 0 or w*h > 2147483647:
            raise ValueError('Invalid dimensions: ' + name)
        if len(m['walkable']) != w*h or any(type(x) is not bool for x in m['walkable']):
            raise ValueError('Wrong Walkable length/type: ' + name)
        for cell in [m['entry'], m['exit'], *[[c['x'], c['y']] for c in m['spawnCells']]]:
            if len(cell) != 2 or any(type(x) is not int for x in cell) or not (0 <= cell[0] < w and 0 <= cell[1] < h):
                raise ValueError('Cell out of bounds: ' + name)
        modules[name] = m
    return modules


def module_reasons(m):
    w, h = m['width'], m['height']
    start = tuple(m['entry'])
    if not m['walkable'][start[1]*w+start[0]]:
        return ['Entry cell is blocked']
    reasons = []
    if m['kind'] in ('Combat', 'Boss') and not m['spawnCells']:
        reasons.append('Combat/Boss requires a spawn')
    seen = {start}
    queue = collections.deque([start])
    while queue:
        x, y = queue.popleft()
        for a, b in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if 0 <= a < w and 0 <= b < h and m['walkable'][b*w+a] and (a, b) not in seen:
                seen.add((a, b))
                queue.append((a, b))
    if tuple(m['exit']) not in seen:
        reasons.append('Exit blocked/unreachable')
    for spawn in m['spawnCells']:
        if (spawn['x'], spawn['y']) not in seen:
            reasons.append('Spawn blocked/unreachable: ' + str(spawn))
    return reasons


def validate(ids, modules):
    reasons = []
    if len(ids) != 5:
        reasons.append('Expected five segments')
    for i, name in enumerate(ids):
        if name not in modules:
            reasons.append('Unknown module: ' + name)
            continue
        m = modules[name]
        if m['kind'] != ('Boss' if i == 4 else 'Combat'):
            reasons.append('Wrong kind: ' + name)
        reasons.extend(name + ': ' + reason for reason in module_reasons(m))
    return reasons


def generate(seed, modules):
    combat = sorted((n for n, m in modules.items() if m['kind'] == 'Combat'), key=lambda n: n.encode('utf-16-be', 'surrogatepass'))
    bosses = sorted((n for n, m in modules.items() if m['kind'] == 'Boss'), key=lambda n: n.encode('utf-16-be', 'surrogatepass'))
    if not combat or not bosses:
        return dict(seed=seed, success=False, ids=[], fallback=False, attempts=0, reason='Missing Combat/Boss')
    state = seed & MASK

    def index(n):
        nonlocal state
        while True:
            state = (state + 0x9e3779b9) & MASK
            z = state
            z = ((z ^ (z >> 16))*0x85ebca6b) & MASK
            z = ((z ^ (z >> 13))*0xc2b2ae35) & MASK
            z ^= z >> 16
            if z >= ((-n) & MASK) % n:
                return z % n

    for attempt in range(1, 21):
        ids = [combat[index(len(combat))] for _ in range(4)] + [bosses[index(len(bosses))]]
        reasons = validate(ids, modules)
        if not reasons:
            return dict(seed=seed, success=True, ids=ids, fallback=False, attempts=attempt, reason='validated')
    last = reasons
    ids = ['street_a', 'street_b', 'street_c', 'street_a', 'street_boss']
    reasons = validate(ids, modules)
    return dict(seed=seed, success=not reasons, ids=ids if not reasons else [], fallback=not reasons,
                attempts=20, reason='last rejection: ' + ' | '.join(last) + '; fallback: ' + (' | '.join(reasons) or 'validated'))


def sweep(modules, count):
    records = []
    start = time.perf_counter()
    for seed in range(count):
        result = generate(seed, modules)
        if result['success']:
            assert len(result['ids']) == 5 and not validate(result['ids'], modules), result
        else:
            assert result['reason'].strip() and not result['ids'], result
        records.append(result)
    return records, time.perf_counter() - start


def fixture_examples(data):
    narrow = dict(id='narrow', kind='Combat', width=3, height=3,
                  walkable=[False]*3+[True]*3+[False]*3, entry=[0, 1], exit=[2, 1], spawnCells=[dict(x=1, y=1)])
    assert not module_reasons(narrow)
    blocked = copy.deepcopy(narrow)
    blocked['walkable'][3] = False
    assert module_reasons(blocked) == ['Entry cell is blocked']
    broken = copy.deepcopy(data)
    boss = next(m for m in broken['modules'] if m['id'] == 'street_boss')
    boss['walkable'][boss['spawnCells'][0]['y']*boss['width']+boss['spawnCells'][0]['x']] = False
    modules = catalog_data(broken)
    assert any('Spawn' in r for r in module_reasons(boss))
    failed = generate(0, modules)
    assert not failed['success'] and failed['reason'].strip()
    return {'narrow': 'valid grid only; physical clearance unverified', 'blockedEntry': module_reasons(blocked),
            'disabledBossSpawn': module_reasons(boss), 'failureExample': failed}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog', required=True)
    parser.add_argument('--count', type=int, default=1000)
    parser.add_argument('--output', default='Game/TestResults')
    parser.add_argument('--reference-examples', action='store_true', help='Only for the supplied six-id reference fixture')
    args = parser.parse_args()
    if not __debug__:
        parser.error('Do not run with -O: validation assertions must remain enabled')
    if args.count < 1000:
        parser.error('At least 1000 seeds are required')
    try:
        path, output = checked_path(args.catalog), checked_path(args.output)
        raw = path.read_bytes()
        data = json.loads(raw)
        modules = catalog_data(data)
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.error(str(error))
    before = copy.deepcopy(data)
    records, seconds = sweep(modules, args.count)
    assert before == data, 'Input mutated'
    examples = fixture_examples(data) if args.reference_examples else 'not requested'
    report = dict(scope='Python reference only; C# and Unity NOT RUN', python=sys.version, os=sys.platform,
                  architecture=platform.machine(), catalog=str(path.relative_to(ROOT)),
                  catalogSha256=hashlib.sha256(raw).hexdigest(), generatorVersion=1, contentVersion=1,
                  seedRange=[0, args.count-1], elapsedSeconds=seconds,
                  timingScope='Reference generation + independent per-success validation; excludes JSON IO/examples',
                  successes=sum(r['success'] for r in records), failures=sum(not r['success'] for r in records),
                  fallbacks=sum(r['fallback'] for r in records), distinctLayouts=len({tuple(r['ids']) for r in records if r['success']}),
                  moduleReview={n: module_reasons(m) for n, m in modules.items()}, examples=examples, records=records)
    destination = output / ('s305-reference-' + uuid.uuid4().hex[:8])
    destination.mkdir(parents=True, exist_ok=False)
    (destination / 'review.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: v for k, v in report.items() if k != 'records'}, indent=2))
    print('REPORT ' + str((destination / 'review.json').relative_to(ROOT)))


if __name__ == '__main__':
    main()
