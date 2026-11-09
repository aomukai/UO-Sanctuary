#!/usr/bin/env python3
"""Authenticate the focused, approved Ward rollout without historical matrix reruns."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from prepare_psychic_ward_native import prepare
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/psychic-ward-cast'
def record(check=False):
    inputs={}
    def pin(path):
        path=ROOT/path if isinstance(path,str) else path
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_text()
    approval=pin('tools/reference/psychic-ward-reaction-approval.json')
    assert approval['approved'] and approval['approval']=='Approve this Psychic Ward rule'
    original=subprocess.check_output(['git','show',approval['candidateCommit']+':'+approval['proposal']],cwd=ROOT)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    pin(STAGE/'source-snapshot-2.json')
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    for kind,number in [('native',1),('content',2),('integration',2)]:
        log=pin(STAGE/f'{kind}-build-{number}.log')
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for kind in ['Content','Integration']:
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for path in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert e.sha(STAGE/'native/Distribution'/path)==born['artifacts'][path]
    prepare(True);native=pin(STAGE/'native-source-evidence.json');pin(native['patch'])
    parent=ROOT/'.local/class-skills/player-melee-return/native'
    old={str(p.relative_to(parent)):e.sha(p) for p in (parent/'Projects').rglob('*.cs') if 'obj' not in p.relative_to(parent).parts and 'bin' not in p.relative_to(parent).parts}
    new={str(p.relative_to(STAGE/'native')):e.sha(p) for p in (STAGE/'native/Projects').rglob('*.cs') if 'obj' not in p.relative_to(STAGE/'native').parts and 'bin' not in p.relative_to(STAGE/'native').parts}
    assert old.keys()==new.keys()
    assert {n for n in old if old[n]!=new[n]}==set(native['changedSources'])
    for name in ['ModernUO.dll','Server.dll']:
        assert e.sha(parent/'Distribution'/name)==born['artifacts'][name]
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'ward-cast-check':1,'ward-cast-reload-check':2,'ward-cast-right-check':3,'ward-cast-right-result':3,'ward-cast-quick-check':3,'ward-cast-quick-result':3})
    log=e.cohort(STAGE,expected,24);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert log.count('Ward automatic incoming:11native AOS packets')==6
    assert log.count('Ward original:Warlock/Witch captured rank/stun')==3
    fixture=pin(STAGE/'ward-cast-fixtures.json');assert fixture['Generation']==2
    assert [o['Female'] for o in fixture['Owners']]==[False,True]
    assert [o['State']['Remaining256'] for o in fixture['Owners']]==[641,642]
    failed=pin(STAGE/'diagnostics-1/birth-native-checks.log');assert 'Ward learned/right/quick/self/town metadata' in failed and 'ward-cast-check: failed' in failed
    for name in ['birth-inputs.json','PsychicWardCastChecks.cs','native-run-1.log']:pin(STAGE/'diagnostics-1'/name)
    report=pin('data/Sanctuary/Balance/class-skill-status.json');rows=report['skills']
    assert sum(r['implementation']=='handler-present' for r in rows)==214
    assert sum(r['implementation']=='handler-present' and r['talentId'].startswith('warlock.') for r in rows)==5
    ward=next(r for r in rows if r['talentId']=='warlock.psychicward');assert ward['implementation']=='handler-present' and not ward['complete']
    protected=e.protection('tools/reference/psychic-ward-cast-client-evidence.json',43)
    client=pin('tools/reference/psychic-ward-cast-client-evidence.json');pin(client['inputManifest']['path'])
    for name,digest in client['files'].items():assert e.sha(ROOT/name)==digest,name
    for name in ['tools/record_psychic_ward_cast.py','tools/prepare_psychic_ward_native.py','tools/prepare_psychic_ward_client.py','tools/review_psychic_ward_client.py','tests/check_psychic_ward_cast.py','docs/psychic-ward-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/class-skill-roadmap.md','tools/build_skill_roadmap.py']:pin(name)
    result=dict(referenceVersion='3.3.93847',approved=True,learnedEnabled=True,productionCaptureEnabled=True,handlers=214,warlockHandlers=5,completedClasses=[],currentEventParity=False,nativeMarkers=24,markers=dict(expected),boots=3,reloads=2,saves=3,originalSavedOwners=2,nativePacketCasesPerOwner=11,connectedInputs=6,nativeBuild=1,clientBuild=1,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,newTimers=0,newSavedFormats=0,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,optionalAudit=False,artifacts=born['artifacts'],preservation={**protected,'installed':False},limits=['Explicit approved Sanctuary event rule;protected current event37/40 parity unverified.','Graphical input/rendering,PvP,Energy Shield coexistence,legacy non-AOS and special native damage modifiers remain unqualified.','No deployment or final effects/icons;normal demon startup remains unapproved.'],inputs=inputs)
    path=ROOT/'tools/reference/psychic-ward-cast-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Ward admitted:214handlers/Warlock5;24markers,6connected casts,2original owners/2reloads;installed106/saves38/config20 preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
