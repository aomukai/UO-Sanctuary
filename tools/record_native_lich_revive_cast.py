#!/usr/bin/env python3
"""Bind approved native Lich admission to ordinary learned input and saves."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/native-lich-revive-cast';P='8c64dd335ff6370b0afc47209611d13ae03ed6ef'
def record(check=False):
    inputs={}
    def pin(path):
        path=R/path if isinstance(path,str)else path;inputs[str(path.relative_to(R))]=e.sha(path)
        return e.load(path)if path.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-1.json',S/'birth-inputs.json');pin(S/'source-snapshot-1.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/native-lich-revive/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/NativeLichReviveInputChecks.cs'}and not a.keys()-b.keys()
    assert{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/NativeReviveRules.cs'}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in('NativeCorpseChecks','NativeCorpseDifficultyChecks','NativeLichReviveChecks')}
    assert born['copiedData']==old['copiedData']
    before=subprocess.check_output(['git','show',P+':src/Sanctuary.Content/NativeReviveRules.cs'],cwd=R).decode()
    assert(R/'src/Sanctuary.Content/NativeReviveRules.cs').read_text()==before.replace('Approved=>false','Approved=>true')
    for kind in('content','integration'):
        path=S/f'{kind}-build-1.log';pin(path);assert all(x in path.read_text()for x in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    approval=pin('tools/reference/native-lich-revive-world-rule-approval.json');assert approval['approved']and approval['candidateCommit']==P
    assert approval['answer']=='Approve the complete native Lich rule (Recommended)'and approval['questionItemId']=='["request_user_input_async","call_ce2abc8961904b2ebf799f174d06f78c",0]'
    parent=pin(approval['testedCandidateEvidence']);assert e.sha(R/approval['proposal'])==approval['proposalAtApprovalSha256']and e.sha(R/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256'];pin(approval['proposal'])
    expected=Counter(parent['markers'])
    for mode in('normal-right','normal-quick','nightmare-right','hell-quick'):expected.update({f'native-lich-revive-{mode}-check':3,f'native-lich-revive-{mode}-result':3})
    log=e.cohort(S,expected,87);pin(S/'birth-native-checks.log');pin(S/'native-run-1.log');assert log.count('Client: 127.0.0.1: Connected.')==30
    for mode in('normal-right','normal-quick','nightmare-right','hell-quick'):assert log.count(f'native-lich-revive-{mode}: windup')==3
    for mode in('ce-right','poison-quick','nightmare-ce-right','hell-poison-quick'):assert log.count(f'native-corpse-{mode}: windup')==3
    for n in('native-lich-revive','native-corpse','native-corpse-difficulties','raised-minions'):assert pin(S/f'{n}-fixtures.json')['Generation']==2
    assert len(pin(S/'corpse-base-fixtures.json')['Rows'])==4
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in('src/Sanctuary.Content/NativeReviveRules.cs','tests/Sanctuary.Integration/NativeLichReviveInputChecks.cs','tests/Sanctuary.Integration/NativeCorpseChecks.cs',
        'tests/Sanctuary.Integration/NativeCorpseDifficultyChecks.cs','tests/Sanctuary.Integration/NativeLichReviveChecks.cs','tests/check_native_lich_revive_cast.py','tools/record_native_lich_revive_cast.py',
        'docs/native-lich-revive-cast-checkpoint.md','docs/native-corpse-sources.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned native Lich Revive',handlers=236,completedClasses=[],nativeLichReviveAdmitted=True,policyApproved=True,
        contentBuild=1,integrationBuild=1,contentSourceSnapshot=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=87,markers=dict(expected),authenticatedSessions=30,actualInputs=24,nativeReviveInputs=12,
        nativeReviveInputCases=dict(normalRight=3,normalQuick=3,nightmareRight=3,hellQuick=3),retainedExplosionInputs=12,nativeLichLiveAbilityControls=3,boots=3,reloads=2,saves=3,
        artifacts=born['artifacts'],approval='tools/reference/native-lich-revive-world-rule-approval.json',newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),frozenPendingFiles=frozen,
        verified=['Twelve ordinary right/quick learned casts activate the actual original Lich in Normal,Nightmare,Hell through natural world updates,with once-only release/payment and retained loot.',
            'Native identity,AI eligibility analogue,life context,allegiance,no-regen and permanent roster membership match the approved rule.',
            'Twelve retained explosion inputs,actual original firebolt/raise/blind weapon/map travel/helper cleanup/parking and two-reload active/inactive original controls retain.'],
        limits=['Other unclassified native species,old missing-state corpses,unsupported elites/controllers/equipment and inherited nonzero Sunder remain outside admitted scope.',
            'Native host cadence/load and in-flight missile ownership are explicit approved adaptations;global monster-spell conversion remains separate.',
            'No whole-class,PvP,graphical or final-asset certification;four older Warlock proposals remain frozen.'],inputs=inputs)
    path=R/'tools/reference/native-lich-revive-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Native Lich learned C1/I1/S1/N1:87markers/30connections/12native+12retained inputs/2reloads;approved native original admitted.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
