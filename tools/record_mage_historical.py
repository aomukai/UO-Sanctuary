#!/usr/bin/env python3
"""Bind historical Mage adoption through the shared ranged controller checks."""
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
STAGE = ROOT / '.local/class-skills/mage-historical'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/mage-elements-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ['IronGolemWorld','MageOffensiveSkills','MagePetBuffs','MageWorldBody','MinionWardrobeProfiles','NecromancerGroundWorld','NecromancerMageMissiles','NecromancerSummons','PersistentMinions']}|{'tests/Sanctuary.Integration/MageMissileChecks.cs'}
    added={'src/Sanctuary.Content/MageWorld.cs','tests/Sanctuary.Integration/MageHistoricalChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/mage-elements/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['mage-historical-check']+=1;expected['mage-historical-reload-check']+=2;expected['mage-historical-live-result']+=3
    assert sum(expected.values())==274 and markers(STAGE/'birth-native-checks.log')==expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('AI67:1598 complete source ordered-service/state/draw comparisons') == 3
    assert log.count('Mage target services:actual learned actor,distinct supplied ranged primary,15/16and19/20boundaries') == 3
    assert log.count('Historical Mage:three original types share ranged clock/actions/all fixed secondaries/Enchant/armor')==1
    assert log.count('Mage elements:3slot/4096default samples,learned three-element births/zero alias')==1
    assert log.count('Mage elements reload:8 original actors/two poison conversions')==2
    assert log.count('Mage Meteor:hard9+soft1 refusal/hard10 fixed10')==1
    assert log.count('Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts and shared Phoenix offset consumer pass')==3
    assert log.count('Freeze state:12096 application,1296 removal,100 callback and224 length source/native cases passed')==3
    assert log.count('Mage Static:44800 one-draw/exclusive five-percent policy contexts')==1
    assert log.count('Mage buffs:hard19+real soft1 refuses;live hard20 unlock/respec/relearn')==1
    assert log.count('Mage world:ranged AI1,S1end24/think25,A1kind2action35/end45') == 1
    assert log.count('4096 complete selector and2160 complete shared charged-bolt') == 3
    assert log.count('4032 complete source compositions') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 876), ('first-loop-affix-audit.json', 21)]:
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
    docs = ['mage-historical-checkpoint', 'skeletal-mage-upgrade-plan', 'pet-adjustment-review', 'class-skill-reuse-notes', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_mage_historical.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs], ROOT / 'tools/reference/sentry-target-source-evidence.json', ROOT/'tools/reference/tornado-pulse-source-evidence.json', ROOT/'tools/build_class_skills.py', ROOT/'data/Sanctuary/Balance/necromancer-summons.json']
    names=[f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','pre-build-1.json','content-build-1.log','integration-build-1.log','run-1.log','audit-run-1.log','attempt-1-birth-inputs.json','attempt-1-birth-native-checks.log','attempt-1-audit-inputs.json','attempt-1-audit-native-checks.log','birth-inputs.json','birth-native-checks.log','audit-inputs.json','audit-native-checks.log','python-tests.log','named-readiness.log','catalogue-check.log','mage-historical-fixtures.json','named-item-runtime-audit.json','first-loop-affix-audit.json']
    files += [STAGE/name for name in names]
    saved=json.loads((STAGE/'mage-historical-fixtures.json').read_text());assert saved['Generation']==2 and saved['State']['Controller']['Ai']['Parameter0']==1 and saved['DeferredState']['Controller']['Ai']['Parameter0']==1
    # Actor layouts are unchanged: optional legacy clock data lives in the
    # existing Mage JSON persistence rather than new fields on saved mobiles.
    import re,subprocess
    for path in ['src/Sanctuary.Content/NecromancerSummons.cs','src/Sanctuary.Content/MageWorldBody.cs']:
        prior_source=subprocess.check_output(['git','show','de5d166:'+path],cwd=ROOT,text=True)
        current_source=(ROOT/path).read_text()
        pattern=r'\[SerializableField\([^]]+\)\]\s*private[^;]+;'
        assert re.findall(pattern,prior_source)==re.findall(pattern,current_source)
        assert re.findall(r'\[SerializationGenerator\([^]]+\)\]',prior_source)==re.findall(r'\[SerializationGenerator\([^]]+\)\]',current_source)
    result=dict(referenceVersion='3.3.93847',component='Historical Mage actors share the existing saved ranged world/AI/spell behavior',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=274,retainedMageElementMeteorGlacialChainStaticBuffPlayerPetSentryMarkers=268,boots=3,actualReloads=2,saves=3,naturalAutomaticHistoricalAdoptionSessions=3,retainedAdoptedBasicProjectileSessions=3,savedOriginalActors=2,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[876,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'MageWorld extracts one shared ranged behavior from the learned subtype. Iron ground conductor resolves that behavior for original NecromancerMinion Mages as well;all target,path,AI,clock,seed,movement,regen,proc and spell consumers remain shared. Golems and other minions retain their original paths.',
            'Historical ground records extend existing MageMissileSave JSON with optional LegacyWorlds,defaulting empty for older payloads. Both actor version0 layouts/serialized fields remain identical. Atomic restore validates flights,seeds and ground records before replacing any state;load reattaches real actors/owners and cancels pending actions without replay.',
            'Existing minion resume/sweep and legacy think hooks adopt eligible world Mages without cloning,rank/resource recapture,HP write or roster-order reset. Missing old source clocks enter neutral frame0/think1 rather than replay emergence;existing actor seeds are retained and absent seeds allocate once. Native summon/melee timers are suppressed to prevent duplicate attacks.',
            'All three original elemental types use the ranged kind2 clock and fixed Glacial/Meteor/Chain/Static plus Enchant/intrinsic armor. Original five-percent policy slots and packets remain in their existing consumers. Deterministic tests cover identity/wounds/maxima/order/seed preservation,idempotence,atomic refusal,older JSON and non-Mage exclusion.',
            'Two original actors survive2reloads:an adopted pending windup cancels to neutral without damage/RNG replay,a deferred actor adopts through owner resume. Controlled real controller resumption launches and collides without changing inspected original state after restoration. Three natural automatic adoption sessions cast Enchant/Meteor and deal actual ground damage;retained historical basic sessions use the same400ms skill action instead of the retired native timer. All268 predecessor markers,71Python,4audits and named/catalogue checks pass;data,audit decisions,kernel and106installed hashes stay unchanged.'],
        remaining=[
            'Neutral adoption is an explicit host transition because old native actor data did not save a source-frame phase;it does not reconstruct lost engine history. Optional JSON/default/layout proofs and candidate saved actors are not an actual installed old-DLL byte upgrade or full concurrent/unheld save acceptance.',
            'Exact modern target/path/room/size/filter/global seed/event/NHD,special/PvP/Sunder,source removal and client behavior retain prior shared-controller limits. Old native RNG cannot be recovered;absent actor streams use the established host supplier. Existing saved streams are preserved.',
            'Basic Sorceress replacement curves,hard5triples and recorded Skeleton Mastery mapping remain required. Element creation/conversion and fixed-rank upgrades are bound;historical AI adoption is enabled within this checkpoint scope. Other approved pets,safe-area parking,barrier phases and remaining class skills remain queued.',
            'No class completion,deployment,new player binding or final effects/audio/icons. Shared bodies/cast/projectile/field placeholders stay identified;old actors retain their type and identity.'])
    output = ROOT / 'tools/reference/mage-historical-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Historical Mage:274markers/3boots/2reloads,original actors/shared AI/spells,3natural adoption sessions;268retained/4audits/71Python;installed unchanged. Mage package remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
