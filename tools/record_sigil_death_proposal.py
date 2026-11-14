#!/usr/bin/env python3
"""Bind a complete prepared Sigil Death rule without enabling learned input."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sigil-death-proposal'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else None if path.suffix=='.dll'else path.read_text()
    primary=pin('tools/reference/d2r-named-equipment/json/skills.json');hidden=pin('data/Sanctuary/Balance/sigil-death.json');assert hidden==[dict(ReferenceVersion='3.3.93847',Skill=400,Explosion=403,Fields=primary['403'])]
    source=pin('tools/reference/sigil-death-reference-gap.json');assert not source['eventCandidate']['dispatcherLinked']and not source['learnedHandlerEnabled']
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    before=pin(STAGE/'source-snapshot-3.json')['inputs'];after=pin(STAGE/'source-snapshot-5.json')['inputs']
    assert before['projectInputs']==after['projectInputs']and before['copiedData']==after['copiedData']
    assert {n for n in before['sourceInputs']if before['sourceInputs'][n]!=after['sourceInputs'][n]}=={'tests/Sanctuary.Integration/SigilDeathChecks.cs'}
    for kind,number in [('content',2),('integration',4)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for name in ['sigil-death','sigil-family','corpse-town']:expected[name+'-check']=1;expected[name+'-reload-check']=2
    for name in ['sigil-death','sigil-family']:expected[name+'-check-result']=1;expected[name+'-reload-check-result']=2
    log=e.cohort(STAGE,expected,24);assert log.count('Death candidate:shared paid marker/pulse')==3
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    saved=pin(STAGE/'sigil-death-fixtures.json');assert saved['Generation']==2 and saved['Actor']['Rank']==200 and saved['Actor']['Frames']==5100 and saved['Actor']['DeathPulse']>=0 and saved['Life']%256==127
    assert saved['Mark']['Seed']!=0 and saved['Mark']['Modifiers']==saved['Actor']['DeathModifiers']
    assert pin(STAGE/'sigil-family-fixtures.json')['Generation']==2
    for run in [1,2,3]:
        for name in ['birth-inputs.json',f'native-run-{run}.log','birth-native-checks.log','SigilDeathChecks.cs','Sanctuary.Integration.dll']:pin(STAGE/f'diagnostics-{run}'/name)
    assert 'loot=False criminal=True'in (STAGE/'diagnostics-2/birth-native-checks.log').read_text()
    assert 'CS8752'in pin(STAGE/'integration-build-3.log');pin(STAGE/'diagnostics-3/reason.txt')
    parent=pin('tools/reference/pet-birth-startup-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==220 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==11
    candidate=pin('src/Sanctuary.Content/SigilDeathProposal.cs');assert 'Approved=>false'in candidate and 'public static void Configure'not in candidate and 'CorpseExplosionPackets.Create'not in candidate
    for name in ['src/Sanctuary.Content/SigilActors.cs','src/Sanctuary.Content/CorpseSkillRules.cs','tests/Sanctuary.Integration/SigilDeathChecks.cs','tests/check_sigil_death.py','tools/record_sigil_death_proposal.py','docs/sigil-death-rule-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='complete prepared Sigil Death rule candidate',approved=False,handlers=220,warlockHandlers=11,completedClasses=[],learnedDeathEnabled=False,productionPulseEnabled=False,productionDeathHookEnabled=False,nativeMarkers=24,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=4,sourceSnapshot=5,nativeRun=4,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalNullOmittedMetadata=['SigilActor.DeathModifiers','SigilActor.DeathPulse','SigilActorSave.DeathMarks'],optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Complete explicit unapproved adaptation for event33/34 and do153;current dispatcher/recipient code remains unqualified.','Connected fixture owners,actual shared marker driver/native corpses/queued chains are tested;learned authenticated input is not enabled or claimed.','Saved recipient deadline is held;graphical,PvP,full real-gear and unsupported-receiver acceptance remain open.','No old do55 level scaling or inner physical radius is substituted;generic corpse/percentage/RNG/defense helpers are reused.'],inputs=inputs)
    path=ROOT/'tools/reference/sigil-death-proposal-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Sigil Death candidate evidence stale'
    else:path.write_text(encoded)
    print('Death candidate:24markers/3boots/2reloads;actual corpses/loot/queued chain and original mark/cursor/wounds pass;220handlers,production and learned route gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
