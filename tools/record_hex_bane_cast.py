#!/usr/bin/env python3
"""Authenticate approved learned Bane,legacy saves and shared Hex regressions."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/hex-bane-cast'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str)else path;inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_text()
    approval=pin('tools/reference/hex-bane-damage-approval.json');assert approval['approved']and approval['approval']=='Approve this Bane rule'
    raw=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(raw).hexdigest()==approval['proposalAtApprovalSha256']
    group=pin('tools/reference/weapon-hex-groups-approval.json');assert group['approved']
    frozen=pin('tools/reference/hex-bane-damage-proposal-native-evidence.json');assert frozen['nativeMarkers']==12 and frozen['newReferenceReadBytes']==560
    # Authenticate changed component source against its frozen candidate commit,
    # not against the later approved/live workspace.
    for name,h in frozen['inputs'].items():
        if name.startswith(('src/','shared/','tests/'))and name.endswith('.cs'):
            assert hashlib.sha256(subprocess.check_output(['git','show',approval['candidateCommit']+':'+name],cwd=ROOT)).hexdigest()==h,name
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    before=pin(STAGE/'source-snapshot-1.json')['inputs'];after=pin(STAGE/'source-snapshot-2.json')['inputs'];assert before['projectInputs']==after['projectInputs']and before['copiedData']==after['copiedData']
    assert {n for n in before['sourceInputs']if before['sourceInputs'][n]!=after['sourceInputs'][n]}=={'tests/Sanctuary.Integration/HexBaneCastChecks.cs'}
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',1),('integration',2)]:
        log=pin(STAGE/f'{kind}-build-{number}.log');assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,**{f'{name}-check':1 for name in ['bane-cast','siphon-cast','hex-group']},**{f'{name}-reload-check':2 for name in ['bane-cast','siphon-cast','hex-group']},**{f'{name}-{mode}-{suffix}':3 for name in ['bane-cast','siphon-cast']for mode in ['right','quick']for suffix in ['check','result']}})
    log=e.cohort(STAGE,expected,42);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    for message,count in [('Bane learned:selfSC/right/quick',2),('Bane events:direct/native/ordinary post-result misses',6),('Bane original:Warlock/Witch',3),('Siphon events:native/class melee misses',6),('Hex group candidate:9buff transitions,6cross-family target transitions',6)]:assert log.count(message)==count,message
    fixture=pin(STAGE/'bane-cast-fixtures.json');assert fixture['Generation']==2 and [r['Female']for r in fixture['Rows']]==[False,True]and all(r['Buff']['CastMastery']==50 for r in fixture['Rows'])
    for name in ['siphon-cast-fixtures.json','hex-group-fixtures.json']:assert pin(STAGE/name)['Generation']==2
    old=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'Bane modified defense before accuracy roll'in old and 'bane-cast-reload-check: failed'in old
    for name in ['birth-inputs.json','native-run-1.log','HexBaneCastChecks.cs']:pin(STAGE/'diagnostics-1'/name)
    parent=pin('tools/reference/hex-siphon-cast-native-evidence.json')
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43);client=pin('tools/reference/hex-siphon-cast-client-evidence.json');pin(client['inputManifest']['path'])
    for name,h in client['files'].items():assert e.sha(ROOT/name)==h,name
    report=pin('data/Sanctuary/Balance/class-skill-status.json');rows=report['skills'];assert sum(r['implementation']=='handler-present'for r in rows)==217 and sum(r['implementation']=='handler-present'and r['talentId'].startswith('warlock.')for r in rows)==8
    assert next(r for r in rows if r['talentId']=='warlock.hexbane')['implementation']=='handler-present'and next(r for r in rows if r['talentId']=='warlock.hexpurge')['implementation']=='blocked'
    source=pin('src/Sanctuary.Content/HexBane.cs');assert 'WhenWritingDefault' in source and 'CastMastery' in source and 'base("SanctuaryHexBane",183)'in source and 'writer.Write(0)'in source
    for name in ['HexBaneEvents','WeaponHexDelivery','HexSiphonEvents','ClassStats','PlayerWeaponContact','PreparedWeaponEvents','WarlockCombat','HexBaneDamageProposal']:pin('src/Sanctuary.Content/'+name+'.cs')
    assert 'Approved=>true'in (ROOT/'src/Sanctuary.Content/HexBaneDamageProposal.cs').read_text()
    for name in ['tests/Sanctuary.Integration/HexBaneCastChecks.cs','tests/check_hex_bane_cast.py','tools/record_hex_bane_cast.py','tools/build_skill_roadmap.py','docs/hex-bane-cast-checkpoint.md','docs/hex-bane-damage-proposal.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md']:pin(name)
    result=dict(referenceVersion='3.3.93847',approved=True,learnedEnabled=True,currentSelectorCallbackParity=False,handlers=217,warlockHandlers=8,completedClasses=[],nativeMarkers=42,markers=dict(expected),boots=3,reloads=2,saves=3,baneConnectedInputs=6,siphonConnectedInputs=6,savedBaneOwners=2,capturedMastery=[50,50],oldZeroJsonRead=True,optionalJsonMetadata='CastMastery',persistenceIdChanged=False,serializationVersionChanged=False,newTimers=0,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newLiveReferenceReads=0,priorCandidateReferenceBytes=560,oldLargeOfflineMatricesRerun=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Approved two-stage mastery/post-result rule,not recovered current selector28cc10/callback36parity.','Nonzero cast registrations are explicit prepared metadata in native checks;live mastery-granting gear remains unqualified/gated by its own item work.','Exact ranged capture/impact timing,advanced hextrigger producers,special overrides,full PvP/graphical input remain unqualified.','Purge and normal demon startup remain separate;effects/icons stay pending.'],inputs=inputs)
    path=ROOT/'tools/reference/hex-bane-cast-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Bane admitted:217handlers/Warlock8;42markers,12connected Bane/Siphon casts,2reloads;oldJSON and captured metadata pass;installed files preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
