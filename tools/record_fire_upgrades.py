#!/usr/bin/env python3
"""Bind approved Fire upgrades to frozen native/build/audit evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/fire-upgrades'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/f'{n}-inputs.json') for n in ('birth','audit')}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/f'{n}-inputs.json')
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/iron-golem-spectral-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('FireGolemAura','FireGolemBody','GolemPetDamage','ItemDurationDefenses','NecromancerSummons','PaladinElements')}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs' for n in ('FireGolemAuraChecks','FireGolemCombatChecks','FireGolemWorldChecks')}
    added={'src/Sanctuary.Content/FireGolemUpgrades.cs','tests/Sanctuary.Integration/FireGolemUpgradeChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed and inputs['birth']['copiedData']==old['copiedData']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/iron-golem-spectral/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['fire-upgrades-check']+=1;expected['fire-upgrades-reload-check']+=2;expected['fire-upgrades-live-result']+=3
    assert sum(expected.values())==382 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Fire upgrades:shared200percent Clay curve/no old fire,')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in (('named-item-runtime-audit.json',885),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert len(audit['InputHashes'])==count and audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest
        previous=json.loads(subprocess.check_output(['git','show','c0dda8d:tools/reference/'+n],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ': failed' not in (STAGE/'audit-native-checks.log').read_text() and 'exported from the selected build' in (STAGE/f'audit-run-{audit_run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest
    saved=json.loads((STAGE/'fire-upgrades-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Actors'])==4 and len({a['Type'] for a in saved['Actors']})==4
    assert all(a['Life']<a['Maximum'] for a in saved['Actors']) and sum(a['Seed'] is not None for a in saved['Actors'])==3
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==186 and sum(s['characterClass']=='Necromancer' for s in handlers)==30 and not status['completedClasses']
    docs=('fire-upgrades-checkpoint','pet-adjustment-review','class-skill-reuse-notes','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons')
    files=[parent,baseline,Path(__file__).resolve(),ROOT/'tests/check_fire_upgrades.py',ROOT/'tools/reference/iron-affix-reuse-review.json',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    names=('birth-inputs.json','birth-native-checks.log','audit-inputs.json','audit-native-checks.log','fire-upgrades-fixtures.json','named-item-runtime-audit.json','first-loop-affix-audit.json','python-tests.log','named-readiness.log','catalogue-check.log')
    files += [STAGE/n for n in names];files += [p for p in STAGE.iterdir() if p.is_file() and p.name.startswith(('pre-build-','content-build-','integration-build-','run-','audit-run-','attempt-'))]
    result=dict(referenceVersion='3.3.93847',component='Approved Fire200percent Clay,own-rank Holy Fire,live hard10CBF/hard20breakable cold immunity',installed=False,goalComplete=False,completedClasses=[],fireUpgradePackageComplete=False,approvedFireDamageEnabled=True,approvedFireAuraEnabled=True,approvedFireDefensesEnabled=True,build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=382,retainedIronBloodMagePlayerPetSentryMarkers=376,boots=3,actualReloads=2,saves=3,independentHolyFireWeaponGoldens=5,savedActorTypes=4,actualFacetTransitions=3,naturalPulseThenMixedMeleeSessions=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[885,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/f'{n}-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/f'{n}-inputs.json')) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Fire reuses captured Skeleton/Clay with Golem Mastery and200percent intrinsic endpoints before shared ordinary percentage/critical/item/aura consumers. Old innate physical and skill-fire/follow-up damage are replaced. Rank4/no Mastery physical goldens are547..1094FP256.',
            'Holy Fire uses captured effective Fire Golem rank and zero Paladin synergies through the shared weapon lease/pulse. Five Paladin weapon goldens and revised actual rank4pulse/critical/Enchant/mitigation/shield/cancel/return/saved-lease controls pass. Historical plain pulse reuses the same emitter with Utility RNG;prepared actors retain their saved source stream. Original source birth fields remain inactive damage reference outputs.',
            'Live hard10CBF uses the item-property consumer;ordinary cold/freeze are blocked while cold damage and Holy Freeze remain separate. Hard20adds a100cold resistance floor,preserves stronger existing resistance,and allows shared one-fifth Conviction to break100to83. Hard9+soft1/19+soft1refuse;train/respec/relearn do not recapture curve/aura,heal or change controller/order/seed. Three facets preserve wounded life ratios.',
            'Four existing actor types/resources/curve/ranks and three saved source streams survive two real reloads. Three connected sessions verify the existing50frame first pulse at zero mana,live hard10/20defenses,and later actual mixed melee. Both rebuilds,382native markers retaining376,four audits,71Python/573named/eight-class240pass;885/21fresh fingerprints with decisions/data/kernel/installed106unchanged. No new serialization field,deployment or class completion.'],
        remaining=[
            'Existing50frame initial-pulse adapter/current activation/event priority,optional monster element/mode RNG,full native fallback/historical pulse scheduling,room/geometry/PvP/client equivalence remain explicit acceptance gaps. Original source birth proof is not reinterpreted as proof of the approved replacement.',
            'Historical absorption/death policy differences,complex own-item interactions and installed old-DLL/concurrent/unheld-save acceptance retain earlier limits. Existing status expiry rules are not reset by training/respec.',
            'Mage basic/triple/Mastery mapping,barriers,parking,Druid/remaining classes and other reference/acceptance gaps remain. Body/contact/pulse/cold-defense/audio and talent/hotkey artwork remain identified placeholders in separate registers. No class is complete.'])
    output=ROOT/'tools/reference/fire-upgrades-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Fire upgrades:382markers/3boots/2reloads,four saved types,shared damage/aura/cold defenses;installed unchanged. No class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('build','run','audit-run'):p.add_argument('--'+name,type=int,required=True)
    p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
