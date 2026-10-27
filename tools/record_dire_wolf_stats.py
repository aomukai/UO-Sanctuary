#!/usr/bin/env python3
"""Bind prepared Dire birth/mode stats to their exact source and native package."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/dire-wolf'
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log and log.count(': passed') == 151
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('dire-wolf-stats-check: passed') == 1
    assert '55152complete source comparisons including72nonzero-sunder transport controls' in log
    assert 'Prepared beast attack-mode stats match12288source contexts' in log
    for name in ['barbs-casting', 'barbs-aura', 'barbs-world', 'gem-activation', 'prepared-weapon-event', 'wolverine-casting', 'wolverine-aura', 'wolverine-world', 'oak-casting', 'oak-world']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for family in ['barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3
    assert log.count('barbs-world-live-result: passed') == 3
    assert 'Prepared Dire Wolf birth:55152native comparisons' in (STAGE / 'stats-run.log').read_text()
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'barbs-casting-fixtures.json', 'barbs-aura-fixtures.json', 'barbs-world-fixtures.json']]
    source_cases = {}
    for key, name in [('creation', 'dire-wolf-creation'), ('rage', 'fenris-rage'), ('mode', 'druid-beast-attack')]:
        path = ROOT / f'tools/reference/{name}-engine-evidence.json'
        evidence = json.loads(path.read_text())
        assert evidence['referenceVersion'] == '3.3.93847' and evidence['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
        fixture = evidence['fixture']
        assert sha(ROOT / fixture['path']) == fixture['sha256']
        assert sha(BASE / (name + '-source-probe.cpp')) == fixture['generatedSourceSha256']
        assert (BASE / (name + '-sanitizer.log')).read_text() == ''
        for source, digest in evidence['sourceHashes'].items():
            assert sha(BASE / 'd2moo-pinned-source' / source) == digest, source
        source_cases[key] = fixture['cases']
        artifacts += [path, ROOT / fixture['path'], BASE / (name + '-source-probe.cpp'), BASE / (name + '-sanitizer.log')]
    assert source_cases == dict(creation=55158, rage=960, mode=12736)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
        artifacts += [STAGE / name, ROOT / 'tools/reference' / name]
    baseline = json.loads(subprocess.check_output(['git', 'show', '69f3336:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 170 and sum(x['characterClass'] == 'Druid' for x in handlers) == 14
    assert status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summondirewolf')['implementation'] != 'handler-present'
    observation = json.loads((ROOT / 'tools/reference/druid-beasts-user-observations.json').read_text())['observed']['direWolf']['tooltip']
    assert [observation[k] for k in ['effectiveRank', 'manaCost', 'attackRatingPercent', 'life', 'minimumDamage', 'maximumDamage', 'maximumWolves', 'maximumLifePercent', 'defensePercent']] == [28, 20, 700, 1803, 635, 762, 3, 455, 392]
    artifacts += [ROOT / path for path in [
        'src/Sanctuary.Content/DruidDireWolfStats.cs', 'src/Sanctuary.Content/DruidBeastAttackStats.cs',
        'tests/Sanctuary.Integration/DireWolfStatsChecks.cs', 'tests/check_dire_wolf_stats.py', 'tests/check_creeper_stats.py',
        'tools/record_dire_wolf_stats.py', 'tools/probe_dire_wolf_creation_reference.py', 'tools/probe_fenris_rage_reference.py', 'tools/probe_druid_beast_attack_reference.py',
        'tools/prepare_dire_wolf_reference.py', 'tools/reference/dire-wolf-preflight.json', 'tools/reference/druid-beasts-user-observations.json',
        'tools/build_class_skills.py', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json', 'data/Sanctuary/Balance/necromancer-summons.json']]
    result = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, handlers=170, druidHandlers=14, completedClasses=[], goalComplete=False,
        verified=dict(nativeBirthComparisons=55152, nonzeroSunderTransportControls=72, nativeModeComparisons=12288, sourceCases=source_cases,
            rankControls=255, corroboratedUserTooltip=observation, boots=3, retainedActualReloads=2, passMarkers=151, retainedBarbsRightCasts=3, retainedBarbsQuickCasts=3,
            retainedNaturalBarbsRuns=3, pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes'])),
        testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Dire Wolf prepared birth reuses explicit-level and percentage services, matching55,152complete source contexts over255ranks,three difficulties/owner levels,two game contexts,independent effective wolf/bear ranks and fractional base life. Four copied passives/resists,20mana,cap3,54FP base regeneration and requested equipment level are distinct from live attack-mode values.',
            'Actual trained Druid/Tarnhelm/Grizzly,immutable earlier birth and captured-rank difficulty reconstruction pass. Current physical-sunder stat is transported by the correct pierce-immunity-damage gear key and retained by numeric rescale; no native immunity delivery is claimed.',
            'Prepared shared Spirit/Fenris mode-list values match12,288complete source compositions. A1adds monster AR50/150 to live skill AR75/150+20/rank after the first; rank-zero skill AR is zero. This mode list is separate from birth monlvl/passive AR. Whole physical endpoints and clearing/replacement are checked; native world mode entry is not connected.',
            'User rank28tooltip independently corroborates1803displayed life,635..762damage with prior effective Grizzly28,700AR/392defense,455displayed life bonus,20mana/cap3. Actual birth additionally includes10percent life per own rank; tooltip ln12is not the entire multiplier.',
            'Separate960case complete corpse/rage source composition establishes search versus release predicate distinctions,consume-before-buff allocation,500frame/+100percent buff and no direct health/mana write. This is not a native corpse actor or AI consumer.',
            'Retained learned spirits,resource/item/gem/weapon checks pass151markers across three disposable-world boots/two actual reloads,including three Barbs right/quick casts and natural follow/aura/expiry runs. Builds have zero warnings/errors;70Python checks and fresh item audits preserve573named decisions.',
        ], limits=[
            'No Dire Wolf body,Fenris AI,corpse ownership/consumption/rage stat list,physical combat,learned placement/payment/cancellation/input or Dire save/reload acceptance is enabled. Retained reloads belong to existing actors. No new installed package or completed class is claimed.',
            'The source mode-list result exposes an existing Spirit Wolf attack-rating consumer gap: current hit chance uses birth base/passive AR without this separate live mode AR. Correct native A1entry capture/save/aggregation and targeted regression are next; do not silently certify the installed chance as source-complete.',
            'Nonzero physical-sunder delivery,summon equipment,downstream physical-floor behavior,full incoming HP/death/event chronology,modern AI/timing/geometry/path/RNG and graphical acceptance remain separately open. Complete old routines with current tables are bounded source evidence,not a modern executable parity certificate.',
            'Final WF body/motion/corpse-feeding/rage/teleport/contact/audio assets are explicitly pending. Talent/right/quick icons remain in the separate text-label register. Installed main69f3336and existing saves/config/client are preserved.',
        ])
    path = ROOT / 'tools/reference/dire-wolf-stats-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Dire preparation:55152birth/12288mode comparisons,151markers/3boots/2retained reloads;170handlers/Druid14unchanged. Actor/AI/rage/native mode consumers remain unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
