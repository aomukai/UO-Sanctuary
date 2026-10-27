#!/usr/bin/env python3
"""Bind the partial weapon recovery phase to exact focused/full tested inputs."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/weapon-event-phase'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    focused = json.loads((STAGE / 'stats-inputs.json').read_text())
    full = json.loads((STAGE / 'class-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    for inventory in [focused, full]:
        assert {p for p in inventory['sourceInputs'] if p.endswith('.cs')} == expected
        for name, digest in inventory['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        for name, digest in inventory['copiedData'].items():
            assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    assert focused['artifacts'] == full['artifacts'] and focused['copiedData'] == full['copiedData']
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == focused['artifacts']['Assemblies/' + name]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert '0 Warning(s)' in build and '0 Error(s)' in build
    logs = [(STAGE / name).read_text() for name in ['stats-native-checks.log', 'class-native-checks.log']]
    for log in logs:
        assert ' ERR]' not in log and ': failed' not in log
        assert log.count('Shutting down') == 3 and log.count('Writing world save snapshot done') >= 3
    assert logs[0].count('prepared-weapon-event-check: passed') == 1
    assert logs[0].count('prepared-weapon-event-reload-check: passed') == 2
    assert logs[0].count(': passed') == 119
    for name in ['life-tap', 'paladin-thorns', 'smite']:
        assert name + '-check: passed' in logs[1], name
        assert logs[1].count(name + '-reload-check: passed') == 2, name
    assert 'Prepared recovery phase:' in (STAGE / 'stats-run.log').read_text()
    assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((STAGE / ('full-' + name)).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    saved = json.loads((STAGE / 'prepared-weapon-event-fixtures.json').read_text())
    assert saved['OwnerLife'] == 2560 and saved['TargetLife'] == 7680
    assert saved['Thorns']['Percent'] == 4 and saved['Thorns']['DamagePercent'] == 250
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 169
    assert status['completedClasses'] == []
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'class-inputs.json', 'stats-native-checks.log', 'class-native-checks.log', 'stats-run.log', 'full-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'prepared-weapon-event-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'full-named-item-runtime-audit.json', 'full-first-loop-affix-audit.json']]
    artifacts += [ROOT / name for name in ['tests/check_prepared_weapon_events.py', 'tests/check_creeper_stats.py', 'tools/review_weapon_event_order.py', 'tools/reference/weapon-event-order-review.json', 'tools/reference/retaliation-event-order-gap.json', 'tools/record_prepared_weapon_events.py']]
    gem_gap=ROOT / '.local/class-skills/weapon-event-gems/stats-native-checks.log'
    assert 'Actual weapon strike did not leech once from physical loss' in gem_gap.read_text()
    artifacts += [gem_gap, gem_gap.parent / 'run.log', gem_gap.parent / 'stats-inputs.json']
    result = dict(referenceVersion='3.3.93847', installed=False, newHandlerEnabled=False, handlers=169, druidHandlers=13, completedClasses=[], currentExecutableParity=False,
        additionalGemRegression=dict(status='failed-obsolete-whole-HP-test-expectation', expectationUpdateStaged=True, nextBranch='codex/druid-barbs-actor', nextComponentStillNeedsVerification=True),
        scope='Partial recovery ordering before the existing retaliation bridge. Full incoming HP/death/event chronology remains unfinished; no Barbs actor/handler enabled.',
        verified=dict(focusedBoots=3, focusedActualReloads=2, focusedPassMarkers=119, fullBoots=3, fullActualReloads=2, fullPassMarkers=logs[1].count(': passed'), pythonTests=70),
        sourceInputs=focused['sourceInputs'], copiedData=focused['copiedData'], testedAssemblies=focused['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Native AOS, typed weapon damage and root GearEffects apply Life Tap before retaliation, including otherwise lethal return. Explicit captured and ordinary item leech use clipped FP256 physical damage before return. Ranged/miss/zero controls and two real aura/curse/resource reloads pass.',
            'Physical eligibility is captured before absorb healing; recovery cannot expand the return percentage basis. Crushing Blow remains before Life Tap on root/Smite routes. Smite gains no ordinary leech.',
            'Retained class suite, learned spirits, fresh item audits and all70Python checks pass against the same exact C#/data/DLL package. No installed-world or graphical-client acceptance is inferred.',
        ], remaining=[
            'Incoming HP subtraction must precede flat/percentage return in the pinned complete source. The current bridge still returns before native/content life commitment; complete lethal/mutual-death, kill/status/event and mode chronology need a proper shared bridge.',
            'Native AOS absorb placement, full victim mana/stamina/status tails, every proc/item/summon producer and complete current executable event behavior are not certified.',
            'The extra gem-activation fixture retains an obsolete whole-HP root-leech expectation and fails; its fractional expectation update is staged in the separate Barbs worktree, not included or certified by this frozen package.',
            'Spirit of Barbs flat ATD aggregation, actor/aura/casting and actual client behavior remain unfinished. Existing final effects/audio and separate talent/hotkey icons remain placeholders.',
        ])
    output = ROOT / 'tools/reference/prepared-weapon-events-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Prepared weapon recovery: focused119/full%d,3boots/2reloads each; broader HP/death chronology still open; installed=False.' % logs[1].count(': passed'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
