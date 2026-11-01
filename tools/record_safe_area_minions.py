#!/usr/bin/env python3
"""Bind safe-area army parking to frozen native/build/audit evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/safe-area-minions'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/f'{n}-inputs.json') for n in ('birth','audit')}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/f'{n}-inputs.json')
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/mage-inspection-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('IronGolemWorld','MercenaryCare','MinionWardrobeProfiles','NecromancerGolems','NecromancerSkeletons','NpcEquipmentRegeneration','NpcOpenWounds','PersistentMinions','PoisonStates','ReviveNativeActivation','TimedEffects')}
    changed.add('tests/Sanctuary.Integration/BloodGolemDamageChecks.cs')
    added={'src/Sanctuary.Content/MinionSafeAreas.cs','tests/Sanctuary.Integration/SafeAreaMinionChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added and {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/mage-inspection/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==388
    expected['safe-area-minion-check']+=1;expected['safe-area-minion-reload-check']+=2;expected['safe-area-minion-live-result']+=3
    assert sum(expected.values())==394 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Safe-area minions:actual region entry/repeated notifications,')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    targeted=ROOT/'.local/class-skills/safe-area-minions-targeted/birth-native-checks.log';counts=markers(targeted)
    assert sum(counts.values())==21 and counts['safe-area-minion-live-result']==3 and counts['safe-area-minion-reload-check']==2 and counts['blood-damage-live-result']==3 and counts['blood-damage-reload-check']==2
    assert ': failed' not in targeted.read_text() and ' ERR]' not in targeted.read_text()
    assert 'pass across3boots/2reloads' in (STAGE/f'targeted-run-{build}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in (('named-item-runtime-audit.json',887),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert len(audit['InputHashes'])==count and audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest
        previous=json.loads(subprocess.check_output(['git','show','2f764ba:tools/reference/'+n],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ': failed' not in (STAGE/'audit-native-checks.log').read_text() and 'exported from the selected build' in (STAGE/f'audit-run-{audit_run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest
    saved=json.loads((STAGE/'safe-area-minion-fixtures.json').read_text());assert saved['Generation']==2 and saved['Life']<saved['Maximum'] and saved['Companion']!=saved['Mage']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==186 and sum(s['characterClass']=='Necromancer' for s in handlers)==30 and not status['completedClasses']
    docs=('safe-area-minions-checkpoint','safe-area-minion-preflight','pet-adjustment-review','class-skill-reuse-notes','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons')
    files=[parent,baseline,targeted,ROOT/'.local/class-skills/safe-area-minions-targeted/birth-inputs.json',Path(__file__).resolve(),ROOT/'tests/check_safe_area_minions.py',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    files += [p for p in STAGE.iterdir() if p.is_file()]
    result=dict(referenceVersion='3.3.93847',component='Approved safe-area persistent army parking,no recovery and explicit healer-menu access',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=394,retainedMarkers=388,boots=3,actualReloads=2,saves=3,targetedNativeMarkers=21,targetedLiveSessions=3,connectedWindupParkingSessions=3,pythonTests=71,namedDefinitions=573,auditMarkers=4,auditFingerprints=[887,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/f'{n}-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/f'{n}-inputs.json')) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Actual SafeZone/active GuardedRegion classification reuses the existing native town predicate,not roofs or ordinary interiors. Location/map hooks,ground admission,CanThink,load/login/resume,stationary sweep,native Revives and completed Necromancer births use the same existing parked roster/slot ledger. No mobile/save layout or runtime-data change.',
            'Wounds,resources,captured ranks/order/seed and actor identity survive entry/repeated notifications/blocked return/actual placement. Slots restore only after a landing exists. Restoration deadlines/units advance without paying whole or newly accrued fractional recovery. Positive item replenishment is masked before signed poison/wound commitment;harmful timing and negative item rates retain existing admission. A lethal overdue entry effect cannot invalidate the follower iteration.',
            'The real healer context-menu reaches owned parked living actors in place with existing access/peace rules. Automatic trade/conversation paths do not gain parked access. Profile-registered named companions are excluded;native offline autostable/ClaimAutoStabledPets remains and is verified across two real reloads while the wounded Mage remains parked.',
            'Three connected sessions enter safety during a natural Mage windup,remain parked beyond release without flight/Meteor/damage/recovery,and return through the location hook.21focused markers/three boots/two reloads plus394full markers retaining388/three saves pass. Four fresh audits,71Python,573named,eight-class240 and clean builds pass;887/21fresh fingerprints preserve decisions. Kernel,data and installed106unchanged;no deployment/class completion.'],
        remaining=[
            'All Druid species,broader custom safe-house/region authoring,other historical controllers,every expiry/respec/class-switch combination,cross-facet boundaries,final client rendering and installed old-DLL/concurrent/unheld-save acceptance remain separate scopes.',
            'Existing native Revive suspension/Blessed receiver admission is preserved;this does not grant a new periodic-damage entitlement. Prior summon/reference/event/PvP/Sunder limits and final effects/audio/icons remain. Continue parking acceptance,barriers and the Necromancer/class roadmap;no class is complete.'])
    output=ROOT/'tools/reference/safe-area-minions-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Safe-area armies:394markers/2reloads,no parked recovery,explicit healer menu;installed unchanged. No class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('build','run','audit-run'):p.add_argument('--'+name,type=int,required=True)
    p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
