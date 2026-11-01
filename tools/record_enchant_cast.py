#!/usr/bin/env python3
"""Bind Enchant source composition, ordinary input and original saved buffs."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/sorceress-enchant-cast'
BUILD = 4
PARENT = '81fc092'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git', 'show', PARENT + ':' + name], cwd=ROOT)


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    snapshot(STAGE / f'pre-build-{BUILD}.json', STAGE / 'birth-inputs.json')
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 431 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_enchant_cast import configuration
    config = configuration(True)
    assert len(config['extra_families']) == 33 and len(config['extra_checks']) == 9
    for name in config['extra_families']:
        assert counts[name + '-check'] == 1 and counts[name + '-reload-check'] == 2
    for name in config['extra_checks']:
        assert counts[name] == 3
    assert len(config['extra_target_input_commands']) == 36
    for name in config['extra_target_input_commands']:
        assert counts[name + '-check'] == counts[name + '-result'] == 3
        assert log.count(' ' + name + ': windup') == 3, name
    for text in ('765 independent rank/hard goldens,60 compiled-source state comparisons',
                 'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                 'Ordered monster release:1616native complete-source compositions',
                 'NPC return:60488complete-source policies/exact traces'):
        assert text in log, text
    saved = json.loads((STAGE / 'enchant-cast-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Self']['Rank'] > 20
    assert saved['Self']['Rank'] == saved['Other']['Rank'] and saved['Self']['Caster'] == saved['Other']['Caster'] == saved['Owner']
    assert saved['Self']['Min256'] == saved['Other']['Min256'] and saved['Self']['Max256'] == saved['Other']['Max256']
    for kind in ('content', 'integration'):
        text = (STAGE / f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        name = f'{kind}/Sanctuary.{"Content" if kind == "content" else "Integration"}.dll'
        assert sha(STAGE / name) == sha(STAGE / 'focused-accepted-3' / name)
    focused = json.loads((STAGE / 'diagnostic-3/birth-inputs.json').read_text())
    focused_log = (STAGE / 'diagnostic-3/birth-native-checks.log').read_text()
    assert focused['artifacts'] == tested['artifacts'] and sum(markers(STAGE / 'diagnostic-3/birth-native-checks.log').values()) == 51
    assert focused_log.count('Shutting down') == focused_log.count('Writing world save snapshot done') == 3
    assert ' ERR]' not in focused_log and ': failed' not in focused_log
    for path, digest in focused['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    assert focused['copiedData'].keys() == tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if focused['copiedData'][p] != tested['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    prior = json.loads((ROOT / '.local/class-skills/sorceress-telekinesis-combat/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name]
    assert prior['copiedData'].keys() == tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if prior['copiedData'][p] != tested['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    source = ROOT / 'tools/reference/enchant-source-evidence.json'
    proof = json.loads(source.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and proof['cases'] == 4320 and proof['sanitizers'] == 'ASan/UBSan clean'
    assert proof['currentRow']['*Id'] == 52 and proof['currentRow']['srvdofunc'] == 25 and proof['currentRow']['aurastatcalc1'] == 'enma'
    for path, digest in proof['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in proof['currentTable'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in proof['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / path) == digest, path
    parent = ROOT / 'tools/reference/telekinesis-combat-native-evidence.json'
    assert parent.read_bytes() == historical(str(parent.relative_to(ROOT)))
    now = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    before = json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    changed = next(x for x in now['skills'] if x['talentId'] == 'sorceress.enchant')
    previous = next(x for x in before['skills'] if x['talentId'] == 'sorceress.enchant')
    assert changed['verification'] == previous['verification'] == 'partial-native-coverage'
    previous['remaining'] = changed['remaining']
    assert now == before and not now['completedClasses']
    audited = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    assert audited['artifacts'] == tested['artifacts'] and audited['copiedData'] == tested['copiedData']
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    assert ' ERR]' not in (STAGE / 'audit-native-checks.log').read_text()
    fingerprints = []
    for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        previous = json.loads(historical('tools/reference/' + name))
        ignore = ('InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath')
        assert {k: v for k, v in audit.items() if k not in ignore} == {k: v for k, v in previous.items() if k not in ignore}
        fingerprints.append(len(audit['InputHashes']))
    text = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text = (STAGE / f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for', 'Eight classes, 240 source skills', 'Class handler coverage, blockers, effects and icons recorded separately.'):
        assert message in text
    assert 'Traceback' not in text
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    files = [Path(__file__).resolve(), ROOT / 'tests/check_enchant_cast.py', ROOT / 'tools/probe_enchant.py', source, parent,
             ROOT / 'tools/build_skill_roadmap.py',
             *(ROOT / 'docs' / name for name in ('sorceress-enchant-cast-checkpoint.md', 'sorceress-enchant-preflight.md', 'sorceress-cold-armor-preflight.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in (f'pre-build-{BUILD}.json', 'birth-inputs.json', 'birth-native-checks.log', f'full-run-{BUILD}.log', 'enchant-cast-fixtures.json', 'diagnostic-3/birth-inputs.json', 'diagnostic-3/birth-native-checks.log', 'diagnostic-run-3.log', 'audit-inputs.json', 'audit-native-checks.log', f'audit-run-{BUILD}.log', 'python-tests.log', f'checks-{BUILD}.log', f'content-build-{BUILD}.log', f'integration-build-{BUILD}.log')),
             *(ROOT / 'tools/reference' / name for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'))]
    result = dict(referenceVersion='3.3.93847', component='Enchant ordinary casting and independent saved buffs', installed=False,
                  graphicalClientVerified=False, completedClasses=[],
                  verified=dict(nativeMarkers=431, focusedMarkers=51, boots=3, reloads=2, saves=3, newServerWireActions=18,
                                retainedServerWireActions=90, rankHardGoldens=765, nativeSourceStateComparisons=60,
                                olderSourceCompositions=4320, fixtureFamilies=33, sharedChecks=9, auditMarkers=4,
                                auditFingerprints=fingerprints, pythonTests=71, unchangedInstalledArtifacts=106),
                  markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['Complete older do25/state-helper composition, current pinned table identity, weaker/equal/stronger state behavior and original equal-rank owner metadata are bounded separately from modern formula/weapon services.',
                               'Ordinary right/quick self/ally/hostile fallback, release-only FP mana, cancellation with recovery, insufficient/invalid guards, real gear/hard-Warmth/positive enhancement and caster-independent recipient snapshots pass. Enchant mouse admission follows its right-only row.',
                               'Original caster, ally, equipped gear, fractional wounds/mana, snapshots, bindings and unextended source deadlines survive two reloads. Retained native/class/minion/ranged Enchant consumers, missile contracts and shared melee/return regression pass in one cohort.',
                               'Focused and final DLL hashes match; only remaining-work metadata changed between their data snapshots. Clean builds, four selected-build audits, Python/readiness/catalogue checks preserve item decisions and106installed artifact hashes. No deployment/class certificate.'],
                  remaining=['Exact current NPC/isatt/alignment equivalence and weapon-item targeting are not mapped by this ordinary mobile adapter. Source allocator/stat/event/formula seams and curse-only branches are not complete modern execution.',
                             'Modern event/RNG/stat and weapon chronology, special/PvP/Sunder/charged/oskill paths, graphical targeting, old-DLL and unheld concurrent saves remain unverified. Unmodified saved deadlines do not claim the whole high-rank duration elapsed during tests.',
                             'Effects/glow/audio and talent/hotkey icons remain separate identified placeholders. Continue cold-armors with shared cast/status/typed-packet/missile helpers; Inferno remains individually deferred.'])
    out = ROOT / 'tools/reference/enchant-cast-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print('Enchant:431combined/51focused markers,three boots/two reloads,4320source/60native state comparisons,18new inputs;four audits/71Python;106installed hashes preserved;remaining modern/client/assets explicit.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
