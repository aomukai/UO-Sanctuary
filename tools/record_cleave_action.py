#!/usr/bin/env python3
"""Authenticate the complete unapproved Cleave action candidate."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/cleave-action-proposal'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-4.json')['inputs']
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'src/Sanctuary.Content/CleaveActionProposal.cs','tests/Sanctuary.Integration/CleaveActionChecks.cs'}
    for kind,number in [('content',2),('integration',4)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'cleave-sweep-check':1,'cleave-sweep-reload-check':2,'cleave-direction-check':1,'cleave-direction-reload-check':2,'cleave-action-check':1,'cleave-action-reload-check':2,'cleave-action-live-result':3})
    log=e.cohort(STAGE,expected,21);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log')
    assert log.count('Cleave action candidate:SQ24/EIAS captured clock,132/240/348degree fans')==3
    assert log.count('cleave-action-live: ready')==3 and log.count('cleave-action-live-result: passed')==3
    saved=pin(STAGE/'cleave-action-fixtures.json');assert saved['Generation']==2 and saved['Life256']%256==127 and saved['Mana256']%256==239
    for name in ['cleave-direction-fixtures.json','cleave-sweep-fixtures.json']:assert pin(STAGE/name)['Generation']==2
    old=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'cleave-action-reload-check: failed'in old and 'Cleave fan/ordinary contacts/reentrant payment'in old and 'cleave-action-live-result: passed'in old
    for name in ['birth-inputs.json','native-run-1.log','cleave-action-fixtures.json','CleaveActionChecks.cs']:pin(STAGE/'diagnostics-1'/name)
    old=pin(STAGE/'native-2/birth-native-checks.log');assert old.count('cleave-action-live-result: passed')==3 and ': failed'not in old
    for name in ['birth-inputs.json','native-run-2.log','cleave-action-fixtures.json','CleaveActionProposal.cs','CleaveActionChecks.cs']:pin(STAGE/'native-2'/name)
    parent=pin('tools/reference/cleave-direction-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    rows=pin('data/Sanctuary/Balance/class-skill-status.json')['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==217 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==8
    assert next(r for r in rows if r['talentId']=='warlock.cleave')['implementation']=='blocked'
    code=pin('src/Sanctuary.Content/CleaveActionProposal.cs');assert 'Approved=>false'in code and 'PlayerWeaponContact.PrepareOrdinary'in code and 'PlayerWeaponContact.Release'in code
    for name in ['src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/SkillCombat.cs','src/Sanctuary.Content/CleavePrepared.cs','src/Sanctuary.Content/CleaveSweep.cs','tests/Sanctuary.Integration/CleaveActionChecks.cs','tests/check_cleave_action.py','tools/record_cleave_action.py','docs/cleave-action-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']:pin(name)
    result=dict(referenceVersion='3.3.93847',approved=False,learnedEnabled=False,preparedAutomaticClock=True,lifecycleHooks=True,currentStartRecipientMathParity=False,handlers=217,warlockHandlers=8,completedClasses=[],nativeMarkers=21,markers=dict(expected),boots=3,reloads=2,saves=3,connectedPreparedNaturalActions=3,actualLearnedWireInputs=0,contentBuild=2,integrationBuild=4,sourceSnapshot=4,nativeRun=3,reusedSequence=24,transientActions=True,newPersistenceIds=0,newTimers=0,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Unapproved complete action adaptation;public/learned input remains gated.','Connected natural tests inject prepared entry;they are not learned skill-packet acceptance.','Start68,math contracts and recipient450780remain unverified;PvP/advanced item combinations remain separate.','Purge needs typed ordered-health/landed-weapon bridges before its learned integration.'],inputs=inputs)
    path=ROOT/'tools/reference/cleave-action-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Cleave candidate:21markers/3natural connected prepared actions/2reloads;SQ24/fan/contact/payment/cancel/replacement/resource controls pass;217handlers,public input gated;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
