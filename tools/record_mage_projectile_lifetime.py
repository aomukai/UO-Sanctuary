#!/usr/bin/env python3
"""Bind the approved original dead-Mage emitter lifetime checkpoint."""
import argparse, hashlib, json, subprocess, sys, tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/mage-projectile-lifetime'
BASE='dfed762f866e5cb723b0f998c62d9cf60051d7d8'
sys.path.insert(0,str(ROOT/'tests'))
from check_mage_projectile_lifetime import configuration

def markers():
    result=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in configuration()['extra_families']:
        result[family+'-check']+=1;result[family+'-reload-check']+=2
    result['mage-projectile-lifetime-check-result']=1;result['mage-projectile-lifetime-reload-check-result']=2
    for name in configuration()['extra_live_commands']:result[name+'-result']=3
    assert sum(result.values())==57
    return result

def prior(path):return json.loads(subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True))

def record(check=False):
    assert 'pass across3boots/2reloads'in (STAGE/'native-run-5.log').read_text()
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/necromancer-requalification/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-10.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    expected=['src/Sanctuary.Content/'+n+'.cs'for n in ('ChainLightning','GlacialSpikes','MageMeteors','MageProjectileRetirement','NecromancerMageMissiles','SorceressEffects')]+['tests/Sanctuary.Integration/MageProjectileLifetimeChecks.cs']
    assert changed==sorted(expected),changed
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind,build in (('Content',3),('Integration',8)):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(s in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    baseline=e.load(STAGE/'failed-baseline-2-native-inputs.json');assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
    with tarfile.open(STAGE/'failed-baseline-2.tar.gz','r:gz')as t:
        text=t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'loss=0/4096;deleted=True'in text
    log=e.cohort(STAGE,markers(),57)
    assert log.count('Client: 127.0.0.1: Connected.')==18 and 'denied by connection filter'not in log and 'Warning: Attempted to delete'not in log
    saved=e.load(STAGE/'mage-projectile-lifetime-fixtures.json');assert saved['Generation']==2 and saved['Shot']['Frame']==2 and saved['Shot']['Packet']['Minimum256']==saved['Shot']['Packet']['Maximum256']==4096
    approval=e.load(ROOT/'tools/reference/mage-projectile-lifetime-approval.json');assert approval['approved']and not approval['implementationVerifiedAtDecision']
    ledger=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert ledger==prior('data/Sanctuary/Balance/class-skill-status.json')and len(ledger['skills'])==240 and not ledger['completedClasses']
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')and audit['Definitions']==prior('tools/reference/named-item-runtime-audit.json')['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    files=[ROOT/'tools/record_mage_projectile_lifetime.py',ROOT/'docs/mage-projectile-lifetime-checkpoint.md',ROOT/'docs/mage-projectile-lifetime-decision.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/necromancer-coverage-matrix.md',ROOT/'docs/skill-effects.md',ROOT/'docs/skill-icons.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'data/Sanctuary/Balance/class-skill-status.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*STAGE.glob('*.tar.gz')]
    result=dict(component='Approved original dead-Mage emitter preserves already-released effects',referenceVersion='3.3.93847',baselineCommit=BASE,contentBuild=3,integrationBuild=8,sourceSnapshot=10,nativeRun=5,nativeMarkers=57,markers=dict(sorted(markers().items())),postDeathPositiveCasesPerBoot=11,ownerRestrictionCasesPerBoot=4,retainedLiveCastSessions=15,newSocketAbilityInputs=0,connections=18,boots=3,saves=3,reloads=2,originalSavedBoltCompletesOnceAfterTwoReloads=True,saveDeletionWarnings=0,artifacts=born['artifacts'],changedSources=changed,runtimeDataChanged=False,nativeRebuilt=False,entitySerializerVersionChanged=False,newPersistenceProfiles=False,newTimers=False,optionalSavedSupplement='SanctuaryMageMissiles.RetiredEmitters;omitted when absent',approvalAdopted=True,exactD2RCasterRemovalVerified=False,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/mage-projectile-lifetime',limits='Actual original dead Mage remains at zero life until all released basic/historical bolts,Glacial,Chain,Meteor warning/fire expire;no new volley or resources. Existing actor critical/credit and owner lifecycle gates retain. Fifteen death/lifecycle cases repeat on three boots;original pre-save bolt lands once after two reloads. Retained focused Mage source/world/flight/live/save scopes pass;prior938combined/162input scope is separate,not rerun here. Old data without optional supplement retain;no historical phases invented. Failed fixture/build/native attempts are frozen and described in checkpoint. Current modern engine timing/geometry/global RNG/NHD/PvP/old-DLL unheld saves/fullclass/client parity and final effects/audio/icons remain explicit; no new artwork or charm/runeword support.')
    e.write('mage-projectile-lifetime-native-evidence.json',result,files,check)
    print('Mage lifetime:57 exact results,15 retained live casts,2 reloads;original saved bolt lands once;installed build and saves preserved.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
