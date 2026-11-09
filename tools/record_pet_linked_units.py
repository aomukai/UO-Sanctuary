#!/usr/bin/env python3
"""Authenticate current linked-child policies and native ownership projection."""
import argparse,json,struct,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pet-linked-units';PARENT='b44d582d7633e6ba997c9e5bd2c7dfb0dac632d8'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    source=pin(ROOT/'tools/reference/pet-linked-units-source-evidence.json');assert source['sourceContexts']==55 and source['machineCodeBytes']==1055
    for name,h in source['artifacts'].items():assert e.sha(ROOT/name)==h,name
    captures={name:pin(ROOT/'.local/class-skills/warlock-runtime-reference'/name)for name in ['callees-pet-fresh-death-return-20261109.json','callees-pet-owner-loss-links-20261109.json','callees-pet-linked-validity-20261109.json','pet-death-functor-20261109.json']}
    for cap in captures.values():
        for name,h in cap['artifacts'].items():assert e.sha(ROOT/name)==h,name
    fresh=captures['callees-pet-fresh-death-return-20261109.json'];assert fresh['bytesRead']==560 and not fresh['records'][0]['allUnwindFragmentsCaptured']and all(not f['captured']for f in fresh['records'][0]['fragments'])
    links=captures['callees-pet-owner-loss-links-20261109.json'];assert links['bytesRead']==3380 and [r['bytes']for r in links['records']]==[553,374,483]and all(r['allUnwindFragmentsCaptured']for r in links['records'])
    valid=captures['callees-pet-linked-validity-20261109.json'];assert valid['bytesRead']==914 and valid['records'][0]['bytes']==177 and valid['records'][0]['allUnwindFragmentsCaptured']
    functor=captures['pet-death-functor-20261109.json'];assert functor['bytesRead']==666 and functor['invokeRva']=='0x334050'and functor['records'][0]['bytes']==21 and functor['indirectInvokeLinkVerified']
    table=pin(ROOT/'.local/class-skills/warlock-runtime-reference/pet-death-functor-20261109-table.bin');assert struct.unpack('<2Q',table)==(0x14005f320,0x140334050)
    diagnostics=[pin(STAGE/f'capture-diagnostics-{i}/result.json')for i in [1,2]]
    for i in [1,2]:pin(STAGE/f'capture-diagnostics-{i}/capture_pet_death_functor.py')
    assert all(d['bytesRead']==560 and not d['codeRead']for d in diagnostics)
    readBytes=sum(c['bytesRead']for c in captures.values())+sum(d['bytesRead']for d in diagnostics);assert readBytes==6640
    policy=pin(ROOT/'src/Sanctuary.Content/GenericPetLinkedUnits.cs').decode();assert 'GenericPetOwnerLoss.Run(child,0,1,bound)'in policy and 'ids[i]'in policy and 'vector.Count=kept'in policy
    for name in ['SourceFrameTimeline.cs','GenericPetOwnerLoss.cs','GenericPetAiEntry.cs','GenericPetAiOverrides.cs','GenericPetAiPreprocess.cs','GenericPetTimedEvents.cs','GenericPetEventDispatch.cs','GenericPetUninterruptible.cs','GenericPetTraversal.cs','GenericPetOwnerIterator.cs','DeathMarkWorld.cs','WarlockCombat.cs','GenericPetStartupProposal.cs','MonsterUninterruptibles.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    assert 'internal const bool Approved=false;'in (ROOT/'src/Sanctuary.Content/GenericPetStartupProposal.cs').read_text()
    old=pin(STAGE/'fixture-diagnostics-1/source-snapshot-1.json')['inputs'];now=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
    assert [n for n in old['sourceInputs']if old['sourceInputs'][n]!=now['sourceInputs'][n]]==['tests/Sanctuary.Integration/GenericPetLinkedUnitChecks.cs']
    before=pin(STAGE/'fixture-diagnostics-1/GenericPetLinkedUnitChecks.cs').decode();assert 'unsupported||JsonSerializer.Serialize'not in before
    assert 'Linked result/boundary differs at14'in pin(STAGE/'fixture-diagnostics-1/birth-native-checks.log').decode();pin(STAGE/'fixture-diagnostics-1/native-run-1.log')
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,build in [('content',1),('integration',2)]:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-{build}.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'pet-linked-units-check':3,'pet-command-target-check':1,'pet-command-target-reload-check':2,'pet-native-status-check':1,'pet-native-status-reload-check':2})
    log=e.cohort(STAGE,expected,18)
    assert log.count('Linked units:55current contexts/1055bytes')==3 and log.count('Linked death composition:qualified owner-loss policy recursively handles each live child once')==3
    assert log.count('Linked units original:actual NPC/player direct masters and typed native registry')==9
    assert log.count('Uninterruptible original:actual saved mode provider drives scheduling guard and AI switch')==9
    assert log.count('Native pet status original:freeze suppresses AI delivery;live stun entry rearms3frames')==9
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    statuses=pin(STAGE/'pet-native-status-fixtures.json');original=pin(STAGE/'pet-command-target-fixtures.json')
    assert statuses['Generation']==original['Generation']==2 and len(statuses['Entries'])==len(original['Entries'])==3
    for i,entry in enumerate(statuses['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+3,Value1=-1-i-21,Value2=(2147483647-i)^0x5a5a5a5a)
    for i,entry in enumerate(original['Entries']):assert entry['Command']['Control']==dict(Value0=-2147483648+i+6,Value1=-1-i-42,Value2=2147483647-i)
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/pet-linked-units-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tests/check_pet_linked_units.py','src/Sanctuary.Content/GenericPetWorldIdentity.cs']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],reviewOnly=True,automaticAiEnabled=False,learnedEnabled=False,sourceContexts=55,machineCodeBytes=1055,
        capturedButUnported=dict(playerDetachBytes=553),newReferenceReadBytes=readBytes,protectedFreshReturn=True,oldLargeOfflineMatricesRerun=0,nativeOwnershipChecks=9,recursiveDeathCompositions=3,newTimers=0,newSavedFormats=0,
        nativeMarkers=18,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Current notification/cleanup/validity plus fixed21byte Invoke match55contexts. Live IDs/cached end,duplicates,stable partition/backing tail,guard/refusal and logical callback release agree. Binding reuses exact qualified owner-loss policy on linked children with killer0/detach1;three compositions handle live children once before parent then clean links.',
            'Existing world identity now projects actual direct NPC owners for linked validity only. Player-only owner-list route still refuses NPC ownership. Nine original demon component checks use controlled native children,typed registry,immediate master changes,duplicate visits and stable removal. Flags/mode/vector remain supplied;staged children cleaned after each check.',
            'Original birth/wounds/mana/commands/control values and retained live mode/freeze/stun checks survive two reloads. Installed build/save/config/client inputs unchanged.'],
        diagnostics=['Fresh393240protected:one bounded attempt reads only560anchor bytes,no unchanged retry. Owner detach553captured for laterwork.',
            'Functor capture first rejected unsupported data sampling after anchors,then found no unwind entry. Used pinned immutable table and verified straight-line32byte leaf sample ending in owner-loss tail jump;exact21byte Invoke retained. Diagnostic tool versions/results retained.',
            'Native1 verifier compared undefined stopped RAX after fatal source boundary. Test-only correction skips return-value comparison when unsupported,retaining trace/state checks. C1reused;exactsource/log/snapshot retainedfixture-diagnostics-1. Native2accepted.'],
        limits=['Stable caller-owned vector storage,declared typed registry/death effects and logical fixed-functor release/allocator/copy services. No arbitrary reallocation,cyclic death graph,destructor or allocator ABI certification.',
            'Native direct-owner projection retains supplied source flags/mode/vector. Actual linked-vector creation/persistence,native recursive death side effects,owner detachment and protected fresh return remain incomplete.',
            'No automatic pump/startup admission,save migration,learned skill,class certificate,deployment or new assets. Startup proposal still pending.'],inputs=inputs)
    path=ROOT/'tools/reference/pet-linked-units-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Linked lifecycle:55current contexts,3recursive compositions/9native owner checks,18markers/two reloads;production AI gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
