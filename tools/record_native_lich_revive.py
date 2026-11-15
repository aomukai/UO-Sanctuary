#!/usr/bin/env python3
"""Bind the complete prepared native Lich Revive world and proposal."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/native-lich-revive';P='36180e2e8bb5f7e1fc0a6df54b6d20c10b85e022'
def record(check=False):
    inputs={}
    def pin(path):
        path=R/path if isinstance(path,str)else path;inputs[str(path.relative_to(R))]=e.sha(path)
        return e.load(path)if path.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');now=snapshot(S/'source-snapshot-5.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-5.json');content=pin(S/'source-snapshot-4.json')['inputs']
    assert content['projectInputs']==now['projectInputs']and content['copiedData']==now['copiedData']
    assert{n for n in content['sourceInputs']if content['sourceInputs'][n]!=now['sourceInputs'][n]}=={'tests/Sanctuary.Integration/'+n+'.cs'for n in('NativeCorpseChecks','NativeLichReviveChecks')}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/native-corpse-difficulties/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/NativeReviveRules.cs','tests/Sanctuary.Integration/NativeLichReviveChecks.cs'}and not a.keys()-b.keys()
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in('NativeCorpseSources','CorpseSkillRules','ReviveBeforeDeath','ReviveOriginals','SourceCorpseIdentity','ReviveCasts','ReviveRegeneration','ReviveFacets','GraveyardUndead','RaisedMinions')}
    assert{n for n in a if a[n]!=b[n]}==changed|{'tests/Sanctuary.Integration/NativeCorpseChecks.cs'}
    assert born['copiedData'].keys()==old['copiedData'].keys()and{n for n in born['copiedData']if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/native-corpse-sources.json'}
    for kind,num in(('content',2),('integration',4)):
        path=S/f'{kind}-build-{num}.log';pin(path);assert all(x in path.read_text()for x in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    native=pin('data/Sanctuary/Balance/native-corpse-sources.json');assert len(native)==1 and native[0]['reviveAiSource']=='unraveler1'and native[0]['reviveRegeneration']==0
    assert 'Approved=>false' in(R/'src/Sanctuary.Content/NativeReviveRules.cs').read_text()
    parent=pin('tools/reference/native-corpse-difficulties-native-evidence.json');expected=Counter(parent['markers']);expected.update({'native-lich-revive-check':1,'native-lich-revive-reload-check':2,'native-lich-revive-live-check':3,'native-lich-revive-live-result':3})
    log=e.cohort(S,expected,63);pin(S/'birth-native-checks.log');pin(S/'native-run-4.log');assert log.count('Client: 127.0.0.1: Connected.')==18
    for mode in('ce-right','poison-quick','nightmare-ce-right','hell-poison-quick'):assert log.count(f'native-corpse-{mode}: windup')==3
    saved=pin(S/'native-lich-revive-fixtures.json');assert saved['Generation']==2 and saved['Life256']>0 and saved['State']['Actor']==saved['Actor']
    assert saved['State']['Setup']['RegenerationRate']==0 and saved['State']['Setup']['LifePlan']['Source']=='sanctuary.native.graveyard-lich.v1'
    assert saved['Inactive']['ReviveCreature']['RuntimeType']=='Sanctuary.GraveyardLich'and saved['Inactive']['ReviveSnapshotIssue']is None
    assert pin(S/'native-corpse-difficulties-fixtures.json')['Generation']==pin(S/'native-corpse-fixtures.json')['Generation']==pin(S/'raised-minions-fixtures.json')['Generation']==2
    assert len(pin(S/'corpse-base-fixtures.json')['Rows'])==4
    original=pin(S/'source-snapshot-1.json')['inputs']
    assert{n for n in content['sourceInputs']if n.startswith('src/')and content['sourceInputs'][n]!=original['sourceInputs'][n]}=={'src/Sanctuary.Content/RaisedMinions.cs'}
    first=pin(S/'failed-run-1/birth-inputs.json')
    for folder in('failed-run-1','passed-run-2','failed-run-3'):
        prior=pin(S/f'{folder}/birth-inputs.json');pin(S/f'{folder}/birth-native-checks.log');assert prior['artifacts']['Assemblies/Sanctuary.Content.dll']==first['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert first['copiedData']==born['copiedData']
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in(*sorted(changed),'src/Sanctuary.Content/NativeReviveRules.cs','tests/Sanctuary.Integration/NativeLichReviveChecks.cs','tests/Sanctuary.Integration/NativeCorpseChecks.cs',
        'tests/check_native_lich_revive.py','tools/record_native_lich_revive.py','docs/native-lich-revive-world-rule-proposal.md','docs/native-lich-revive-world-checkpoint.md',
        'docs/native-corpse-sources.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Complete prepared original native Lich Revive world',handlers=236,completedClasses=[],nativeLichReviveAdmitted=False,policyApproved=False,
        contentBuild=2,integrationBuild=4,contentSourceSnapshot=4,sourceSnapshot=5,nativeRun=4,nativeMarkers=63,markers=dict(expected),authenticatedSessions=18,actualInputs=12,nativeReviveInputs=0,
        preparedNativeReleases=4,nativeLichLiveAbilityControls=3,boots=3,reloads=2,saves=3,artifacts=born['artifacts'],newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),frozenPendingFiles=frozen,
        verified=['Prepared release pays once and activates the actual original Lich with native source identity,explicit AI analogue,zero regeneration and conserved corpse loot.',
            'Actual original OnThink/native timers cause firebolt damage and an owned raised helper;forced-blind native weapon swing damages the enemy.',
            'Real cross-map travel rescales original and helper through Nightmare/Hell and normal return,retaining fractional life ratio.',
            'Parking/resume retains original wounds and both identities;actual caster deletion removes the dependent helper.',
            'Saved active wounded original and unused inactive native original retain identity,ownership,setup,loot and wounds through two reloads.',
            'Controlled recovery fixture proves custom source recovery refills damage despite native regen-rate suppression,and shared PreventHealing preserves observations. Retained native/mapped corpses,owned/wild raises and12actual explosion inputs pass;ordinary native Revive remains gated.'],
        limits=['Complete native world proposal is pending;AI analogue,no-regen,native weapon/cadence and dependent-helper mapping are not claims of recovered D2R behavior.',
            'Native action clocks/load startup and in-flight firebolt ownership remain existing host behavior;global monster-spell conversion is separate.',
            'Historical CE timeout release/payment were successful;controlled source recovery can erase fixture damage despite UO regen-rate suppression. Fixtures now prevent healing;exclusive cause of N1 is not established. N3 real map travel exposed missing helper transfer;C2 adds shared suspension/resume.',
            'No whole-class,PvP,graphical,other native-species,old missing-state or nonzero-Sunder acceptance;four older Warlock proposals remain frozen.'],inputs=inputs)
    path=R/'tools/reference/native-lich-revive-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Native Lich world C2/I4/S5/N4:63markers/18connections/12retained inputs/3live original ability controls/2reloads;mapping pending,native learned input0.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
