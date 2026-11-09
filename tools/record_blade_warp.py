#!/usr/bin/env python3
"""Record the authorized Blade Warp estimate and focused native acceptance."""
import argparse,csv,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/blade-warp'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str) else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    approval=pin('tools/reference/blade-warp-adaptation.json');assert approval['approved'] and 'pick your best guesstimation' in approval['approval']
    for name,digest in approval['inputs'].items():assert e.sha(ROOT/name)==digest,name
    with (ROOT/'.local/research/d2r-3.3.93847/skills.txt').open(encoding='utf-8-sig',newline='')as f:source=next(r for r in csv.DictReader(f,delimiter='\t')if r['skill']=='Blade Warp')
    skills=pin('data/Sanctuary/Balance/class-skills.json');skill=next(r for r in skills if r['talentId']=='warlock.bladewarp')
    for name,value in skill['sourceFields'].items():assert str(value).strip('"')==source[name].strip('"'),name
    with (ROOT/'.local/research/d2r-3.3.93847/missiles.txt').open(encoding='utf-8-sig',newline='')as f:missiles={r['Missile']:r for r in csv.DictReader(f,delimiter='\t')}
    missile=missiles['bladewarp'];assert [missile[k]for k in ['*ID','pSrvDoFunc','pSrvHitFunc','Vel','MaxVel','Range','Size','ExplosionMissile']]==['707','1','42','36','36','20','1','bladewarpexplode']
    assert missiles['bladewarpexplode']['*ID']=='692'
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    before=pin(STAGE/'source-snapshot-1.json')['inputs'];after=pin(STAGE/'source-snapshot-3.json')['inputs'];pin(STAGE/'source-snapshot-2.json')
    assert before['projectInputs']==after['projectInputs'] and before['copiedData']==after['copiedData']
    assert {n for n in before['sourceInputs']if before['sourceInputs'][n]!=after['sourceInputs'][n]}=={'tests/Sanctuary.Integration/BladeWarpChecks.cs'}
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,number in [('content',1),('integration',2)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    parent=pin('tools/reference/psychic-ward-cast-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'blade-warp-check':1,'blade-warp-reload-check':2,**{f'blade-warp-{mode}-{suffix}':3 for mode in ['left','right','quick']for suffix in ['check','result']}})
    log=e.cohort(STAGE,expected,30);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert log.count('Blade Warp combat:first hostile collision')==6 and log.count('Blade Warp controls:SC cancellation')==2
    assert log.count('Blade Warp geometry:actual wall stops flight')==2 and log.count('Blade Warp original:Warlock/Witch')==3
    fixture=pin(STAGE/'blade-warp-fixtures.json');assert fixture['Generation']==2 and [r['Female']for r in fixture['Owners']]==[False,True]
    report=pin('data/Sanctuary/Balance/class-skill-status.json');rows=report['skills']
    assert sum(r['implementation']=='handler-present' for r in rows)==215
    assert sum(r['implementation']=='handler-present' and r['talentId'].startswith('warlock.') for r in rows)==6
    warp=next(r for r in rows if r['talentId']=='warlock.bladewarp');assert warp['implementation']=='handler-present' and not warp['complete']
    protected=e.protection('tools/reference/psychic-ward-cast-client-evidence.json',43)
    client=pin('tools/reference/psychic-ward-cast-client-evidence.json');pin(client['inputManifest']['path'])
    for name,digest in client['files'].items():assert e.sha(ROOT/name)==digest,name
    effects=pin('data/Sanctuary/Balance/skill-effects.json');icons=pin('data/Sanctuary/Balance/skill-icons.json')
    assert next(r for r in effects if r['talentId']=='warlock.bladewarp')['generation']=='pending'
    assert any(r['talentId']=='warlock.bladewarp' for r in icons)
    for name in ['src/Sanctuary.Content/BladeWarp.cs','src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/BladeWarpChecks.cs','tests/check_blade_warp.py','tools/record_blade_warp.py','tools/build_skill_roadmap.py','docs/blade-warp-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/class-skill-roadmap.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',approvedEstimate=True,currentDo166Hit42Parity=False,handlers=215,warlockHandlers=6,completedClasses=[],nativeMarkers=30,markers=dict(expected),boots=3,reloads=2,saves=3,connectedInputs=9,savedOriginalOwners=2,contentBuild=1,integrationBuild=2,sourceSnapshot=3,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newTimers=1,newSavedFormats=0,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=approval['limits']+['Flight is transient across restart;cooldown uses the existing persisted store. No claim of saved in-flight teleport restoration.','PvP and actual graphical input/rendering are unqualified.'],inputs=inputs)
    path=ROOT/'tools/reference/blade-warp-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Blade Warp:215handlers/Warlock6;30markers/9connected casts/2reloads;approved estimate,installed build/saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
