#!/usr/bin/env python3
"""Bind shared cold armor casts and the fractional pre-health Frozen reaction."""
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
STAGE = ROOT / '.local/class-skills/sorceress-cold-armor'
BUILD = 3
PARENT = '6c1fd83'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git', 'show', PARENT + ':' + name], cwd=ROOT)


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    snapshot(STAGE / f'pre-build-{BUILD}.json', STAGE / 'birth-inputs.json')
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 476 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_cold_armor import configuration
    config = configuration(True)
    assert len(config['extra_families']) == 35 and len(config['extra_checks']) == 10
    for name in config['extra_families']:
        assert counts[name + '-check'] == 1 and counts[name + '-reload-check'] == 2
    for name in config['extra_checks']:
        assert counts[name] == 3
    assert len(config['extra_target_input_commands']) == 42
    for name in config['extra_target_input_commands']:
        assert counts[name + '-check'] == counts[name + '-result'] == 3
        assert log.count(' ' + name + ': windup') == 3, name
    for text in ('2295 rank/hard goldens,45 complete-source group replacements',
                 'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                 'Ordered monster release:1616native complete-source compositions',
                 'NPC return:60488complete-source policies/exact traces'):
        assert text in log, text
    saved = json.loads((STAGE / 'cold-armor-fixtures.json').read_text())
    assert saved['Generation'] == 2 and len(saved['Entries']) == 3
    assert {e['Armor']['Ability'] for e in saved['Entries']} == {36,39,42}
    for entry in saved['Entries']:
        assert entry['Owner'] > 0 and entry['Orb'] > 0 and entry['Armor']['Rank'] > 20
        assert entry['Mana'] > 0 and entry['Life'] > 0 and entry['Life'] % 256 != 0
    for kind in ('content', 'integration'):
        text = (STAGE / f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        name = f'{kind}/Sanctuary.{"Content" if kind == "content" else "Integration"}.dll'
        assert sha(STAGE / name) == sha(STAGE / 'focused-accepted-2' / name)
    focused = json.loads((STAGE / 'diagnostic-2/birth-inputs.json').read_text())
    focused_log = (STAGE / 'diagnostic-2/birth-native-checks.log').read_text()
    assert focused['artifacts'] == tested['artifacts'] and sum(markers(STAGE / 'diagnostic-2/birth-native-checks.log').values()) == 54
    assert focused_log.count('Shutting down') == focused_log.count('Writing world save snapshot done') == 3
    assert ' ERR]' not in focused_log and ': failed' not in focused_log
    for path, digest in focused['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    assert focused['copiedData'].keys() == tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if focused['copiedData'][p] != tested['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    prior = json.loads((ROOT / '.local/class-skills/sorceress-enchant-cast/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name]
    assert prior['copiedData'].keys() == tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if prior['copiedData'][p] != tested['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    source = ROOT / 'tools/reference/cold-armor-source-evidence.json'
    proof = json.loads(source.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and proof['castCases'] == 450 and proof['freezeCases'] == 2560 and proof['removalCases'] == 4 and proof['sanitizers'] == 'ASan/UBSan clean'
    assert {v['group'] for v in proof['currentStates'].values()} == {1} and len(proof['routineHashes']) == 5
    for path, digest in proof['artifacts'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in proof['currentTable'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in proof['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / path) == digest, path
    parent = ROOT / 'tools/reference/enchant-cast-native-evidence.json'
    assert parent.read_bytes() == historical(str(parent.relative_to(ROOT)))
    now = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    before = json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    for talent in ('sorceress.frozenarmor','sorceress.shiverarmor','sorceress.chillingarmor'):
        changed = next(x for x in now['skills'] if x['talentId'] == talent)
        previous = next(x for x in before['skills'] if x['talentId'] == talent)
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
    assert fingerprints == [892,21]
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
    files = [Path(__file__).resolve(), ROOT / 'tests/check_cold_armor.py', ROOT / 'tools/probe_cold_armor.py', source, parent,
             ROOT / 'tools/build_skill_roadmap.py',
             *(ROOT / 'docs' / name for name in ('sorceress-cold-armor-checkpoint.md', 'sorceress-armor-retaliation-preflight.md', 'sorceress-cold-armor-preflight.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in (f'pre-build-{BUILD}.json', 'birth-inputs.json', 'birth-native-checks.log', f'full-run-{BUILD}.log', 'cold-armor-fixtures.json', 'diagnostic-2/birth-inputs.json', 'diagnostic-2/birth-native-checks.log', 'diagnostic-run-2.log', 'audit-inputs.json', 'audit-native-checks.log', f'audit-run-{BUILD}.log', 'python-tests.log', f'checks-{BUILD}.log', f'content-build-{BUILD}.log', f'integration-build-{BUILD}.log')),
             *(ROOT / 'tools/reference' / name for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'))]
    result = dict(referenceVersion='3.3.93847', component='Cold armor prepared casts and Frozen Armor pre-health reaction', installed=False,
                  graphicalClientVerified=False, completedClasses=[],
                  verified=dict(nativeMarkers=476, focusedMarkers=54, boots=3, reloads=2, saves=3, newServerWireActions=18,
                                retainedServerWireActions=108, rankHardGoldens=2295, nativeSourceGroupComparisons=45,
                                olderCastCompositions=450, olderFreezeCallbacks=2560, olderRemovalCallbacks=4,
                                savedOriginalArmors=3, fixtureFamilies=35, sharedChecks=10, auditMarkers=4,
                                auditFingerprints=fingerprints, pythonTests=71, unchangedInstalledArtifacts=106),
                  markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['Complete older do18/group/list/removal and freeze-only event02 bodies compose with explicit services and clean sanitizers. Pinned noncurse armor states share group1. Same-state weaker recasts replace because the old group member is cleared first.',
                               'Three prepared right-only player casts,release-only FP mana,cancellation/insufficient/dead/internal guards,2295rank/hard goldens and45native source-group contexts pass. Frozen Armor retains cast rank and reads live hard synergies;positive fractional physical and lethal native melee react before HP. Whole-HP post callbacks do not repeat the migrated player event.',
                               'Original three armored players,actual equipped items,fractional wounds/mana,bindings,snapshots,ranks and unextended source deadlines survive two reloads. Older rankless JSON keeps captured values without an invented rank. Bone Armor/Psychic Ward group and prior Sorceress/melee/return scopes pass together.',
                               'Focused/final DLL hashes match,with only remaining-work metadata changed between copied data snapshots. Clean builds,four selected-build audits,71Python/573named/240catalogue/generator checks retain item decisions and106installed hashes. No deployment/class certificate.'],
                  remaining=['Exact source sibling event registration/order and allocator/stat/formula/overlay/event services remain separate from the bounded older callback bodies. Existing host Iron Maiden/Clay Slow/Frozen Armor/Life Tap order is explicit,not modern registry certification.',
                             'Shiver damage remains a floating/generic hit callback;Chilling return travel remains a private unsaved timer. Their retained-rank/live-hard typed packet and saved missile factory/return migration is next. Compatibility whole-HP and Mage intrinsic producers are not the migrated player FP path.',
                             'Modern immunity/uninterruptible/unique/species/PvP/Sunder/charged/oskill/graphical and old-DLL/unheld concurrent-save acceptance remain open. Unmodified saved deadlines do not establish a full high-rank lifetime. Effects/audio and talent/hotkey icons stay separate placeholders.'])
    out = ROOT / 'tools/reference/cold-armor-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print('Cold armor:476combined/54focused markers,three boots/two reloads,2295goldens/45native group comparisons,18new inputs;fractional/lethal Frozen reaction,four audits/71Python;106installed hashes preserved;retaliation/modern/client/assets remain.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
