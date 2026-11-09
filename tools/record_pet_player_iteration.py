#!/usr/bin/env python3
"""Verify current recipient enumeration; keep protected notification explicit."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-player-iteration';PARENT='b52f343aae88478d0f81dc50fe059abde47edb46'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-player-iteration-source-evidence.json');assert source['sourceContexts']==69 and source['machineCodeBytes']==581
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/pet-detach-broadcast-20261109.json')
    assert capture['bytesRead']==1498 and capture['sourceReferencesVerified'] and [r['bytes']for r in capture['records']]==[172,469]
    assert not capture['records'][0]['allUnwindFragmentsCaptured']and not any(f['captured']for f in capture['records'][0]['fragments'])and capture['records'][1]['allUnwindFragmentsCaptured']
    leaf=pin(ROOT/'.local/class-skills/warlock-runtime-reference/services-pet-registry-bucket-leaf-20261109.json');assert leaf['bytesRead']==784 and leaf['records'][0]['bytes']==112 and leaf['records'][0]['boundedLeafReference']and not leaf['records'][0]['allUnwindFragmentsCaptured']
    for row in [capture,leaf]:
        for name,h in row['artifacts'].items():assert e.sha(ROOT/name)==h,name
    diagnostic=pin(STAGE/'capture-diagnostics-1/result.json');assert diagnostic['bytesRead']==560 and not diagnostic['codeRead']
    review=pin(ROOT/'tools/reference/pet-client-sync-review.json');assert review['reviewOnly']and not review['currentCallbackVerified']
    assert e.sha(ROOT/'.local/class-skills/d2moo-source-tree.json')==review['treeSha256']
    for name,fields in review['files'].items():assert e.sha(ROOT/name)==fields['sha256']
    assert e.sha(ROOT/'tools/review_pet_client_sync.py')==review['scriptSha256']
    for name in ['GenericPetAiEntry.cs','GenericPetOwnerIterator.cs','GenericPetOwnerLoss.cs','GenericPetLinkedUnits.cs','GenericPetWorldIdentity.cs','GenericPetTimedEvents.cs','GenericPetUninterruptible.cs','GenericPetOwnerDetach.cs','GenericPetOwnerRemove.cs','WarlockCombat.cs','DeathMarkWorld.cs','GenericPetStartupProposal.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    path='src/Sanctuary.Content/GenericPetSearchWorld.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    after=before.replace('if(subject==0){return 0;}var u=s.Unit(subject);','if(subject==0){return 0;}return HasState(s.Unit(subject),state,s.StateCount);\n    }\n    internal static int HasState(GenericPetSearchWorldUnit u,int state,Func<byte,int> stateCount)\n    {',1).replace('int count=s.StateCount(u.Selector445);if(state>=count','int count=stateCount(u.Selector445);if(state>=count')
    assert pin(ROOT/path).decode()==after
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ['content','integration']:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-1.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-player-iteration-check':3,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-native-status-check':1,'pet-native-status-reload-check':2})
    log=e.cohort(STAGE,expected,18)
    assert log.count('Player iteration:69current contexts/581bytes')==3 and log.count('Player iteration original:actual Warlock/Witch identities resolve through typed registry')==3
    for message in ['Owner removal original:composed normal detach unlinks only actual selected GUID','Owner detach original:actual original GUIDs/typed registry','Linked units original:actual NPC/player direct masters','Uninterruptible original:actual saved mode provider','Native pet status original:freeze suppresses AI delivery']:assert log.count(message)==9,message
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    statuses=pin(STAGE/'pet-native-status-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert statuses['Generation']==original['Generation']==2 and len(statuses['Entries'])==len(original['Entries'])==3
    for i,entry in enumerate(statuses['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+3,Value1=-1-i-21,Value2=(2147483647-i)^0x5a5a5a5a)
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/pet-player-iteration-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_player_iteration.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,automaticAiEnabled=False,learnedEnabled=False,sourceContexts=69,machineCodeBytes=581,newReferenceReadBytes=2842,protectedCallbackBytes=172,oldLargeOfflineMatricesRerun=0,
        originalRecipientChecks=3,retainedOriginalRosterChecks=9,newTimers=0,newSavedFormats=0,nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Complete current469byte enumeration and bounded112byte registry leaf match69source/native contexts. Existing state query is extracted unchanged. Source exclusions7/186,selector/count/root guards and live next/late bucket/duplicate behavior retained.',
            'Three original Warlock/Witch recipient checks use actual typed identity with explicit membership/state views. Callback modifications are observed without altering resources. Nine retained original roster/linked/mode/freeze/stun checks and command/control saves pass through two reloads.',
            'Older pet callback and iterator Git blobs authenticated. Older callback sends synchronization packet;current callback remains protected and its payload/effects are not inferred.'],
        limits=['Registry insertion/order and native state producers are supplied. Current172byte notification callback is not verified or replaced by a no-op. Bounded registry leaf has no primary unwind;this is not mislabeled as a complete unwind group.',
            'No automatic AI/startup admission,native death/world action,save migration,learned skill,class certificate,deployment or new assets. Startup proposal pending;no unchanged protected retry.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-player-iteration-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Player enumeration:69current cases/581bytes,3original recipient checks,18markers/two reloads;protected notification stays explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
