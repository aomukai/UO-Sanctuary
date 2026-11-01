#!/usr/bin/env python3
"""Bind approved Mage Static Field and shared player receiver checks."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/mage-static'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-pet-buffs-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['MageWorldBody','SkillCombat','SorceressCombat']}|{'tests/Sanctuary.Integration/'+n+'.cs' for n in ['MagePetBuffChecks','MageWorldChecks']}
    added={'src/Sanctuary.Content/'+n+'.cs' for n in ['StaticFields','MageOffensiveSkills']}|{'tests/Sanctuary.Integration/MageStaticFieldChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-pet-buffs/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['mage-static-check']+=1;expected['mage-static-reload-check']+=2;expected['mage-static-live-result']+=3
    assert sum(expected.values()) == 226 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Mage Static:44800 one-draw/exclusive five-percent policy contexts')==1
    assert log.count('Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn')==1
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 873), ('first-loop-affix-audit.json', 21)]:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down') == 1
    log = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in log and '\nOK\n' in log
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30 and not status['completedClasses']
    docs = ['mage-static-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_static.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json']
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'fire-golem-world-fixtures.json', 'fire-golem-death-fixtures.json', 'blood-golem-world-fixtures.json', 'clay-golem-world-fixtures.json', 'skeleton-world-fixtures.json', 'mage-static-fixtures.json','mage-pet-buff-fixtures.json','sorceress-skills-checks.json','enchant-checks.json','mage-world-fixtures.json', 'sentry-unit-path-fixture.json', 'mage-birth-fixtures.json', 'mage-missile-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());relative='source/D2Game/src/SKILLS/SkillSor.cpp'
    source=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=source.read_bytes()
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    reviewed={n:hashlib.sha256(function(raw.decode(),n).encode()).hexdigest() for n in ['SKILLS_SrvDo020_StaticField','SKILLS_AuraCallback_StaticField']}
    files.append(source)
    result = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],reviewedNotExecuted=reviewed,referencePages=['https://classic.battle.net/diablo2exp/skills/sorceress-lightning.shtml','https://diabloforfans.com/skill/static-field?lang=enUS'], component='Approved Mage fixed5 Static Field proc and shared player/NPC area receiver', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=226,retainedMageBuffPlayerPetSentryMarkers=220,boots=3,actualReloads=2,saves=3,mageProcPolicyContexts=44800,naturalMageStaticSessions=3,learnedMageInputCasts=6,connectedMageWorldSessions=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[873,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'StaticFields shares caster-centered allegiance/LOS/typed mitigation and exact-life receiver between player Sorceress and approved NPC Static. Difficulty follows designated caster facet; player mana validation/payment remains in the player wrapper. Whole-HP source preparation is retained while fractional receiver life and resistance damage remain FP256.',
            'MageOffensiveSkills reserves distinct five-percent intervals and consumes at most one actor draw only when an eligible route is available.44800 controlled policy contexts pass. Only Static is presently connected; unavailable Meteor/Chain/Glacial slots do not reroll or steal another probability.',
            'An existing learned lightning Mage refuses the hard20 proc at19hard+real soft1 and gains it on actual training. Fixed5 caster radius, immune/far targets, invalid-target no-draw, no player/NPC mana, one actor draw, no basic missile on proc, Normal nonlethality and facet33/50floors pass. A real player release retains once-only cost and excludes mastery.',
            'Three connected natural AI sessions emit Static exactly at the existing action35 as one replacement cast. Pending action34 snapshots preserve wounds/seed/target HP and cancel without a proc or missile across2reloads. All220 predecessor markers,player buffs,pet/sentry/source controls and fresh unchanged audit decisions remain.'],
        remaining=[
            'This is bounded native receiver/proc acceptance, not full modern Static parity. Legacy callback negative-resistance precompensation differs from the current common typed receiver; full current executable pierce/absorb/MDR/event/floor ordering remains explicitly unresolved. Do not treat inspected old callback as a complete modern damage-engine proof.',
            'Normal casts stop nonlethally when integer preparation rounds their damage to zero. Exact current floor/rounding edge cases,source aura enumeration/geometry,special/PvP/Sunder recipients and client animation remain separate.',
            'Meteor10/ChainLightning10/GlacialSpike10 are represented only in the probability policy and are not selected by production until their actual NPC projectile/field/cooldown bindings exist. Historical plain Mage AI rollout,three-element migration,basic Sorceress spells/hard5triples and the recorded Skeleton Mastery mapping question remain required.',
            'Fixed Static has no current local delay; this increment introduces no generalized NPC cooldown persistence. A future delayed secondary must retain its own existing cooldown contract. Existing Mage actor/missile lifetime/seed/event limitations remain.',
            'Inspected held save cohorts prove cancellation and transport,not natural unheld concurrent saves or an old installed-build upgrade. No final art,new player binding,deployment or class completion. Other approved pets,safe-area parking and barrier gaps remain queued.'])
    output = ROOT / 'tools/reference/mage-static-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Mage Static:226markers/3boots/2reloads,44800policy contexts/3natural sessions;220retained,4audits/71Python/installed unchanged. Package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
