#!/usr/bin/env python3
"""Pin isolated Rancor primary consumers, old schema and saved-state checks."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/rancor-primary'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'src/Sanctuary.Content/SourceAttackRate.cs','tests/Sanctuary.Integration/RancorPrimaryChecks.cs'}
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        log=pin(STAGE/f'{kind}-build-2.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    families=['iron-golem-items','npc-resource-callbacks','rancor-primary','skill-damage-event']
    expected=Counter({'iron-golem-birth-check':3,**{n+'-check':1 for n in families},**{n+'-reload-check':2 for n in families}})
    log=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    for marker in ['Rancor primary:rank tiers,actual100->150weapon damage','Damage event bridge:fractional/carry/native AOS','Ordered damage bridge:nested fractional damage/healing']:assert log.count(marker)==3
    old=pin(STAGE/'native-1/birth-native-checks.log');assert ': failed'not in old and old.count(': passed')==15
    oldBorn=pin(STAGE/'native-1/birth-inputs.json');assert {n:h for n,h in oldBorn['sourceInputs'].items()if n.endswith('.cs')}==first['sourceInputs']
    for name in ['native-run-1.log','rancor-primary-fixtures.json','RancorPrimaryChecks.cs','Sanctuary.Content.dll','Sanctuary.Integration.dll']:pin(STAGE/'native-1'/name)
    saved=pin(STAGE/'rancor-primary-fixtures.json');assert saved['Generation']==2 and saved['State']['Rank']==20 and saved['State']['DamagePercent']==145 and saved['State']['AttackRate']==24 and saved['Life256']%256==127
    assert saved['State']['Creator']==saved['Owner']and saved['Curse']['Rank']==1
    assert pin(STAGE/'skill-damage-event-fixtures.json')['Generation']==2
    previous=pin('tools/reference/weapon-landed-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==218 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==9
    rancor=next(r for r in rows if r['talentId']=='warlock.sigilrancor');assert rancor['implementation']=='blocked'and rancor['verification']=='partial-native-coverage'
    primary=pin('src/Sanctuary.Content/SigilRancorPrimary.cs');assert 'SigilActors.Pulse+='not in primary and 'SourceMind.Confuse'not in primary
    for name in ['src/Sanctuary.Content/NecromancerEffects.cs','src/Sanctuary.Content/GearEffects.cs','src/Sanctuary.Content/SourceAttackRate.cs','tests/Sanctuary.Integration/RancorPrimaryChecks.cs','tests/check_rancor_primary.py','tools/record_rancor_primary.py','tools/build_skill_roadmap.py','docs/rancor-primary-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','tools/reference/sigil-rancor-reference-gap.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='prepared Rancor primary damage and attack rate',handlers=218,warlockHandlers=9,completedClasses=[],rancorLearnedEnabled=False,automaticPulseEnabled=False,aiImplemented=False,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalNullOmittedMetadata='NecromancerState.RancorPrimary',optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Caller supplies eligibility and expiry;no current do61 selection/reapplication/difficulty/cadence is inferred.','AI chance/RNG,Confuse and other-AI precedence,group8policy,automatic pulses and learned casting remain unsupported.','Persistence fixture holds expiry;full class and graphical acceptance are not certified.'],inputs=inputs)
    path=ROOT/'tools/reference/rancor-primary-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Rancor primary:15markers/3boots/2reloads;actual damage/native-source speed,curse independence and saved wounds pass;218handlers,AI/learned route gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
