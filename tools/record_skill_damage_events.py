#!/usr/bin/env python3
"""Authenticate the direct-damage bridge without admitting Hex Purge."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/skill-damage-events'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-3.json')['inputs']
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {name for name in first['sourceInputs']if first['sourceInputs'][name]!=last['sourceInputs'][name]}=={'tests/Sanctuary.Integration/SkillDamageEventChecks.cs'}
    final=pin(STAGE/'source-snapshot-4.json')['inputs']
    assert last['projectInputs']==final['projectInputs']and last['copiedData']==final['copiedData']
    assert {n for n in last['sourceInputs']if last['sourceInputs'][n]!=final['sourceInputs'][n]}=={'src/Sanctuary.Content/SkillHealth.cs','src/Sanctuary.Content/SkillDamageEvents.cs'}
    for kind,number in [('content',3),('integration',4)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'skill-damage-event-check':1,'skill-damage-event-reload-check':2})
    log=e.cohort(STAGE,expected,12);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert log.count('Damage event bridge:fractional/carry/native AOS/immune/direct fire/field/aura/return/self-cost/suppression/nested/lethal controls pass')==3
    fixture=pin(STAGE/'skill-damage-event-fixtures.json');assert fixture['Generation']==2 and fixture['Life256']%256==127
    old=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'No independent fixture corridor near 5500,3500'in old and 'skill-damage-event-check: failed'in old
    for name in ['birth-inputs.json','native-run-1.log','source-snapshot-1.json','SkillDamageEventChecks.cs']:pin(STAGE/'diagnostics-1'/name)
    assert 'skill-damage-event-reload-check: passed'in pin(STAGE/'native-2/birth-native-checks.log')
    for name in ['birth-inputs.json','native-run-2.log','skill-damage-event-fixtures.json','SkillDamageEventChecks.cs']:pin(STAGE/'native-2'/name)
    assert 'skill-damage-event-reload-check: passed'in pin(STAGE/'native-3/birth-native-checks.log')
    for name in ['birth-inputs.json','native-run-3.log','skill-damage-event-fixtures.json']:pin(STAGE/'native-3'/name)
    parent=pin('tools/reference/hex-bane-cast-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills']
    assert sum(r['implementation']=='handler-present'for r in rows)==217
    assert sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==8
    assert next(r for r in rows if r['talentId']=='warlock.hexpurge')['implementation']=='blocked'
    for name in ['SkillHealth','SkillDamageEvents','SkillCombat','PaladinThorns','DruidFirestorm','DruidHurricane','DruidArmageddon','DruidMoltenBoulder','NecromancerMageMissiles']:
        pin('src/Sanctuary.Content/'+name+'.cs')
    listeners=[str(p.relative_to(ROOT))for p in (ROOT/'src/Sanctuary.Content').glob('*.cs')if 'BeforeDirectDamage+='in p.read_text()]
    assert listeners==[]
    for name in ['tests/Sanctuary.Integration/SkillDamageEventChecks.cs','tests/check_skill_damage_events.py','tools/record_skill_damage_events.py','docs/skill-damage-events-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='typed pre-life direct damage notification',handlers=217,warlockHandlers=8,completedClasses=[],purgeLearnedEnabled=False,productionSubscribers=0,nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=4,sourceSnapshot=4,nativeRun=4,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Purge mark/charge/chance/mastery/explosion policy remains unresolved and unimplemented.','Local damage classification does not certify current D2R recipient callback coverage.','Native non-opt-in HP paths,advanced producers,special overrides,PvP and graphical acceptance remain unqualified.'],inputs=inputs)
    path=ROOT/'tools/reference/skill-damage-events-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Direct damage bridge:12markers/3boots/2reloads;fractional/lethal/native/exclusion controls and original wounds pass;217handlers,Purge gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
