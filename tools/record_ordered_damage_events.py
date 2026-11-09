#!/usr/bin/env python3
"""Pin ordered direct-damage integration without enabling unapproved Purge."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/ordered-damage-events'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        log=pin(STAGE/f'{kind}-build-1.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,**{n+'-check':1 for n in ['iron-golem-items','npc-resource-callbacks','skill-damage-event','cleave-action']},**{n+'-reload-check':2 for n in ['iron-golem-items','npc-resource-callbacks','skill-damage-event','cleave-action']}})
    log=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert log.count('Ordered damage bridge:nested fractional damage/healing')==3
    assert log.count('Damage event bridge:fractional/carry/native AOS')==3
    assert log.count('Cleave action candidate:SQ24/EIAS captured clock')==3
    saved=pin(STAGE/'skill-damage-event-fixtures.json');assert saved['Generation']==2 and saved['Life256']%256==127
    assert pin(STAGE/'cleave-action-fixtures.json')['Generation']==2
    previous=pin('tools/reference/cleave-cast-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert born['artifacts'][name]==previous['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills']
    assert sum(r['implementation']=='handler-present'for r in rows)==218
    assert sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==9
    assert next(r for r in rows if r['talentId']=='warlock.hexpurge')['implementation']=='blocked'
    listeners=[str(p.relative_to(ROOT))for p in (ROOT/'src/Sanctuary.Content').glob('*.cs')if 'BeforeDirectDamage+='in p.read_text()]
    assert listeners==[]
    for name in ['PreparedMonsterDamage','PlayerWeaponContact','InstantSpellNpcDamage','IronGolemContact','SourceFireFields','NpcElementSpellHit','SkillDamageEvents']:
        pin('src/Sanctuary.Content/'+name+'.cs')
    for name in ['tests/Sanctuary.Integration/OrderedDamageEventChecks.cs','tests/Sanctuary.Integration/SkillDamageEventChecks.cs','tests/check_ordered_damage_events.py','tools/record_ordered_damage_events.py','docs/ordered-damage-events-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',component='ordered pre-life direct damage notification',handlers=218,warlockHandlers=9,completedClasses=[],purgeLearnedEnabled=False,productionSubscribers=0,nativeMarkers=15,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Purge reaction decision and learned weapon/cast/subscription integration remain pending.','Dedicated deletion fixture,advanced producer classification,PvP and graphical acceptance are not qualified.','This shared host bridge does not establish protected D2R callback order.'],inputs=inputs)
    path=ROOT/'tools/reference/ordered-damage-events-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Ordered damage bridge:15markers/3boots/2reloads;generic and actual Cleave controls pass;218handlers,Purge gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
