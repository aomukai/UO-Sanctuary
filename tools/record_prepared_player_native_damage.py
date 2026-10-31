#!/usr/bin/env python3
"""Bind player-source native notification and damage-credit support in the shared ordered core."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/prepared-player-native-damage'
FROZEN='8ab831b'
sha=inventory.sha


def markers(path):return Counter(re.findall(r'\b([a-z0-9-]+): passed\b',path.read_text()))


def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={scope:inventory.validate_inventory(STAGE/(scope+'-inputs.json')) for scope in ['birth','audit']}
    for scope,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(scope+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous_path=ROOT/'tools/reference/poison-dagger-attack-native-evidence.json'
    previous=json.loads(previous_path.read_text());old_ref=previous['inputInventories']['birth']
    assert sha(ROOT/old_ref['path'])==old_ref['sha256']
    old=json.loads((ROOT/old_ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(after)==set(before)
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/PreparedMonsterDamage.cs','src/Sanctuary.Content/PreparedMonsterNativeDamage.cs',
        'src/Sanctuary.Content/PreparedMonsterCredit.cs','src/Sanctuary.Content/IronGolemContact.cs',
        'tests/Sanctuary.Integration/PreparedNativeDamageChecks.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    source_path=ROOT/'tools/reference/poison-dagger-source-evidence.json';source=json.loads(source_path.read_text())
    assert source['cases']==6912 and source['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    for path,digest in source['artifacts'].items():assert sha(ROOT/path)==digest,path
    baseline=ROOT/'.local/class-skills/poison-dagger-attack/birth-native-checks.log'
    assert sha(baseline)==previous['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'prepared-player-native-damage-check':3})
    assert sum(expected.values())==124 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Poison Dagger:6912 complete elemental-roll/shared-poison-lane comparisons')==3
    assert log.count('Prepared Poison Dagger:4miss/hit/shield cases;additive AR')==1
    assert log.count('Native player notifications:256native/ordered')==3
    assert log.count('Native notifications:256native/ordered')==3
    review_path=ROOT/'tools/reference/poison-dagger-attack-source-review.json'
    from review_poison_dagger_attack import build as review
    review(True)
    saved=json.loads((STAGE/'poison-dagger-attack-fixtures.json').read_text())
    assert saved['PaidMana']==55744 and saved['PaidMana']%256==192 and saved['Balance']==12800000
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        audit=json.loads((STAGE/name).read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old_audit=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        assert all(audit[k]==v for k,v in old_audit.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        audits.append(audit)
    assert [len(a['InputHashes']) for a in audits]==[819,21] and len(audits[0]['Definitions'])==573
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installation['installedArtifacts'])==106
    for path,digest in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/path)==digest,path
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text())
    assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous_path,source_path,baseline,review_path,ROOT/'tools/review_poison_dagger_attack.py']
    paths += [ROOT/p for p in [*changed,*set(after).difference(before),'tests/check_prepared_player_native_damage.py','tests/check_prepared_player_native_damage_audits.py',
        'docs/prepared-player-native-damage-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',
        f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','birth-native-checks.log','audit-inputs.json',
        'audit-native-checks.log','python-tests.log','poison-dagger-fixtures.json','poison-dagger-attack-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Player sources in the shared ordered native damage bridge',installed=False,
        goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(retainedPoisonSourceCasesPerBoot=6912,retainedResultCasesPerBoot=16384,nativeMarkers=124,playerNativeComparisonsPerBoot=256,npcNativeComparisonsPerBoot=256,refusalsPerSourcePerBoot=7,boots=3,actualReloads=2,saves=3,rightCasts=3,quickCasts=3,
            retainedVenomSourceCasesPerBoot=10240,missHitShieldCases=4,interruptionCases=5,legacyInitialPoisonRateBridge=True,initialPoisonModernVerified=False,auditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[819,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after).difference(before))},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'The shared ordered core accepts qualified PlayerMobile or BaseCreature sources with an NPC recipient. '
            'Notification and credit helpers use the actual Mobile source;direct player RegisterDamage credit remains native. '
            'The temporary pet-owner fallback is retained only for NPC attackers.',
            '256paired native/ordered comparisons per source kind cover fractional and lethal damage,all four visibility modes, '
            'spell callbacks,paralysis,meditation,credit and mounted recipients. Each source also exercises seven explicit region/reentry refusals.',
            'The player return fixture explicitly supplies source kind0. Source HP commitment and once-only deferred death match '
            'the ordinary native path without replaying incoming Damage. Full actor/world return services are still separate.',
            '124markers retain the predecessor121markers,including the prepared Poison Dagger,composition,Venom,Iron and NPC checks. '
            'The complete unchanged named decisions,client/kernel and installed artifacts remain verified.'],
        remaining=[
            'The actual Poison Dagger actor still needs integration with shared ordered contact and return. '
            'Its current live GearEffects path is not replaced by this foundation increment.',
            'Actual player item/kill events and their source order,native incoming modifiers,region policies and arbitrary NPC '
            'damage overrides need explicit actor adapters. No missing callback is certified by this bridge check.',
            'Current executable,geometry,component/RNG,presentation,charged-item/Blood Mana and initial-poison applicability '
            'retain their earlier limits. No new full-class run,class completion,installation or assets are claimed.'])
    path=ROOT/'tools/reference/prepared-player-native-damage-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Player native bridge:256player+256NPC comparisons per boot,124markers/3boots/2reloads,4audits/71Python;live Poison Dagger integration remains next.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
