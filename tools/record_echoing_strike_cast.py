#!/usr/bin/env python3
"""Authenticate approved Echoing learned inputs,automatic hits and saved state."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/echoing-strike-cast'
PARENT='d250f61343c0c6c93e6e62b7d567290f89c6b27b'

def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p) if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-4.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/echoing-strike-world/birth-inputs.json')
    parent=pin('tools/reference/echoing-strike-world-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')}
    now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'tests/Sanctuary.Integration/EchoingStrikeCastChecks.cs'}
    changed={*{'src/Sanctuary.Content/'+n+'.cs'for n in ('CharacterProfiles','ClassDeath','EchoingStrikeWorld','TimedEffects','WarlockCombat')},
        *{'tests/Sanctuary.Integration/'+n+'.cs'for n in ('EchoingStrikeChecks','EchoingStrikeInputChecks','EchoingStrikeWorldChecks')}}
    assert set(now)-set(old)==added and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert set(born['copiedData'])==set(previous['copiedData'])and{n for n in born['copiedData']if born['copiedData'][n]!=previous['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind in ('content','integration'):
        number=1 if kind=='content'else 4
        p=STAGE/f'{kind}-build-{number}.log';pin(p)
        assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'echoing-strike-inputs-check':1,'echoing-strike-inputs-reload-check':2})
    for family in ('echoing-strike-world','echoing-strike-cast'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    for mode in ('left','right','quick'):
        expected.update({'echoing-strike-cast-'+mode+'-check':3,'echoing-strike-cast-'+mode+'-result':3})
    text=e.cohort(STAGE,expected,42);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log')
    assert text.count('Client: 127.0.0.1: Connected.')==15
    for mode in ('left','right','quick'):assert text.count('echoing-strike-cast-'+mode+': windup')==3
    assert text.count('Echoing learned:SC/FCR/locked aim/all bindings,one payment,early/cancel/late-low mana,no fallback,respec snapshot,Stop retention,safe-area/class/death cleanup pass')==1
    assert text.count('Echoing learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay')==2
    history=[]
    for run in (1,2,3):
        archive=STAGE/f'native-{run}';earlier=pin(archive/'birth-inputs.json')
        pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{run}.log')
        failed='echoing-strike-inputs-reload-check: failed'if run<3 else 'echoing-strike-world-reload-check-result: failed'
        assert failed in(archive/'birth-native-checks.log').read_text()
        for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):
            assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert earlier['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        history.append(str(archive.relative_to(ROOT)))
    for n in ('integration-build-1.log','integration-build-2.log','integration-build-3.log','source-snapshot-1.json','source-snapshot-2.json','source-snapshot-3.json'):pin(STAGE/n)
    original_build=pin(STAGE/'source-snapshot-1.json')
    assert {n:h for n,h in original_build['inputs']['sourceInputs'].items()if n.startswith('src/')}=={n:h for n,h in now.items()if n.startswith('src/')}
    world=pin(STAGE/'echoing-strike-world-fixtures.json');learned=pin(STAGE/'echoing-strike-cast-fixtures.json')
    assert world['Generation']==learned['Generation']==2 and len(world['State']['Flights'])==len(world['State']['Leases'])==2
    assert learned['Life']%256 and learned['Mana']%256
    approval=pin('tools/reference/echoing-strike-world-rule-approval.json')
    assert approval['approved']and approval['answer']=='Approve the complete Echoing Strike rule (Recommended)'and approval['candidateCommit']==PARENT
    assert approval['questionItemId']=='["request_user_input_async","call_ca50dc50c6ca4fc1a75cfa8375509419",0]'
    assert approval['testedCandidateEvidenceSha256']==e.sha(ROOT/approval['testedCandidateEvidence'])
    original=subprocess.check_output(['git','show',PARENT+':'+approval['proposal']],cwd=ROOT)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    current=original.decode().replace('**Pending approval.**','**Approved by the user on2026-11-15 JST.** [Exact decision](../tools/reference/echoing-strike-world-rule-approval.json). The body below is the original approved proposal; implementation/acceptance status is recorded separately.\n\n**Original proposal.**',1)
    assert current==(ROOT/approval['proposal']).read_text();pin(approval['proposal'])
    client=pin('tools/reference/eldritch-blast-cast-client-evidence.json');cm=pin(ROOT/client['inputManifest']['path'])
    assert inputs[client['inputManifest']['path']]==client['inputManifest']['sha256']
    for group in ('patchHashes','sharedInputs'):
        for n,h in cm[group].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added|changed),'tests/check_echoing_strike_cast.py','tools/record_echoing_strike_cast.py','tools/build_skill_roadmap.py',
        'docs/echoing-strike-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.EchoingStrike'in code
    result=dict(referenceVersion='3.3.93847',component='Approved learned Echoing Strike',handlers=229,warlockHandlers=20,completedClasses=[],
        approved=True,learnedEnabled=True,automaticTickEnabled=True,contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=4,retainedFailedRuns=history,
        nativeMarkers=42,markers=dict(expected),boots=3,reloads=2,saves=3,authenticatedSessions=15,actualInputs=9,
        inputModes=['left','right','quick'],actualAutomaticMissileHits=9,graphicalInputVerified=False,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        approval='tools/reference/echoing-strike-world-rule-approval.json',frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=[
            'Complete recorded rule approved;learned left/right/quick input uses original non-self target metadata and existing SC/FCR sessions. Nine real socket casts have unpaid windup,one fractional release payment and actual automatic missile contacts on the existing40ms clock.',
            'Locked initial aim/early/cancel/late-low mana/no fallback,Stop retention,respec snapshot retention,actual safe-area retirement without healing/refund,class-history cleanup and actual player death cleanup pass.',
            'Retained real-item/raw-source packet controls,native fan/return/lease/block/wall/damage/Hex/resource tests pass. Original saved packets/IDs/seeds/motion/clocks/two positive leases retain across two reloads without duplicate damage.',
            'Saved Witch/original dagger/fractional wounds+mana/all three bindings retain;post-load learned casting works without transient replay. Prepared persistence and actual real-time socket actors are separate controls.',
            'N1/N2 old input fixture resources changed by ordinary first regeneration callbacks;I2diagnostic confirms all captured inputs identical. I3gates fixture regeneration callbacks using the shared test hook. N3then exposed an isolated-world test assuming the global ID stayed fixed while other owners cast. I4checks original owner flights/leases plus monotonic global ID,and restores the live global state without rolling IDs back. Production C1unchanged;exact failed sources/artifacts/logs retained. No client change/rebuild,new kernel,timer,profile,save version,live-current read,installed deployment or artwork. Shared client source/44patches,installed106artifacts/38saves/20configs and four pending proposal/evidence pairs remain unchanged.'],
        limits=[
            'Current do166/do44/hit34/damage16,capture/order/InitSteps/global RNG/native-world parity remain unverified;the approved whole rule supplies explicit Sanctuary adaptations.',
            'Unmapped native/special/PvP/class-pet contexts remain refused. Graphical playtesting and broader class/property certification remain open;no class certified.',
            'Prepared saved flights use future inspection dates to test persistence,not a one-hour gameplay lifetime. Nine real-time socket actors are transient and not claimed saved.',
            'Four independent proposals remain pending;final cast/outward-return/contact effects,audio and talent/hotkey icons remain separately missing.'],inputs=inputs)
    out=ROOT/'tools/reference/echoing-strike-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Echoing learned evidence stale'
    else:out.write_text(encoded)
    print('Echoing learned:C1/I4/S4/N4,42markers/15connections/9actual casts+automatic contacts/2reloads;229handlers,Warlock20.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
