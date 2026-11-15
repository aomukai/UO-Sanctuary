#!/usr/bin/env python3
"""Authenticate approved Chain public input and actual automatic head/cloud damage."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/miasma-chain-cast';P='4fe6071256efdae60acb59769d489dbf143493b9'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-1.json',S/'birth-inputs.json');pin(S/'source-snapshot-1.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/miasma-chain-world/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('ActionTiming','MiasmaChainCasting','MiasmaChainWorld','TimedEffects','WarlockCombat')}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('EntropyCloudChecks','MiasmaChainBirthChecks','MiasmaChainLaunchChecks','MiasmaChainWorldChecks')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/MiasmaChainCastChecks.cs'}and not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}==changed
    assert born['copiedData'].keys()==old['copiedData'].keys()and{n for n in old['copiedData']if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for k in ('content','integration'):
        p=S/f'{k}-build-1.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    parent=pin('tools/reference/miasma-chain-world-native-evidence.json');expected=Counter(parent['markers']);expected.update({'miasma-chain-cast-check':1,'miasma-chain-cast-check-result':1,'miasma-chain-cast-reload-check':2,'miasma-chain-cast-reload-check-result':2})
    for mode in ('left','right','quick'):expected.update({'miasma-chain-cast-'+mode+'-check':3,'miasma-chain-cast-'+mode+'-result':3})
    log=e.cohort(S,expected,102);pin(S/'birth-native-checks.log');pin(S/'native-run-1.log');assert log.count('Client: 127.0.0.1: Connected.')==45
    for name in ('miasma-chain-cast-left','miasma-chain-cast-right','miasma-chain-cast-quick','miasma-cast-left','miasma-cast-right','miasma-cast-quick','mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick'):assert log.count(name+': windup')==3
    assert log.count('Chain learned:SC/FCR/current selected aim/all bindings,unarmed,one debit,no replay/cancel/low-mana fallback,respec snapshot,Stop retention,safe/class/death cleanup pass')==1
    assert log.count('Chain learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay')==2
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast'):
        saved=pin(S/(f+'-fixtures.json'));assert saved['Generation']==2
        if f=='miasma-chain-cast':assert saved['Life']%256 and saved['Mana']%256
    approval=pin('tools/reference/miasma-chain-world-rule-approval.json');assert approval['approved']and approval['candidateCommit']==P
    assert approval['questionItemId']=='["request_user_input_async","call_d1ba1caa062e4193bfe4d42f1fdad378",0]'and approval['answer']=='Approve the complete Miasma Chain rule (Recommended)'
    assert e.sha(R/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',P+':'+approval['proposal']],cwd=R);assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending approval.**','**Approved by the user on2026-11-15 JST.** [Exact decision](../tools/reference/miasma-chain-world-rule-approval.json). The body below is the original approved proposal;implementation/acceptance status is recorded separately.\n\n**Original proposal.**',1)==(R/approval['proposal']).read_text();pin(approval['proposal'])
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in (*sorted(changed),'tests/Sanctuary.Integration/MiasmaChainCastChecks.cs','tests/check_miasma_chain_cast.py','tools/record_miasma_chain_cast.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','docs/miasma-chain-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Miasma Chain',approved=True,chainLearnedEnabled=True,automaticChainClockEnabled=True,handlers=232,warlockHandlers=23,completedClasses=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=102,markers=dict(expected),authenticatedSessions=45,boots=3,reloads=2,saves=3,actualInputs=30,actualChainInputs=9,retainedActualMiasmaInputs=9,retainedActualMirroredInputs=12,chainInputCases={'left':3,'right':3,'quickUnarmed':3},graphicalInputVerified=False,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Complete rule approved with exact original proposal/candidate/native hashes;public SC/FCR and explicit HUD timing join existing world clock.','Nine actual left/right/quick-unarmed Chain socket casts show unpaid windup,one fractional debit and distinct automatic head then cloud damage. No manual world tick drives them.','Public live selected aim/unarmed,cancel/low mana/no fallback,respec/Stop snapshots,FCR/HUD versus weapon/IAS/chill,safe-area parking and actual class-history/death cleanup pass.','Original saved Witch/dagger/fractional wounds+mana/all bindings retain and fresh learned action works after two reloads without pending replay.','Prepared link/713maker/geometry/leases/RNG/clocks,legacy optional JSON/atomic restore and full cap/motion/parking/damage/clock/slow/clip controls retain.9Miasma Bolt+12Mirrored actual casts retain.','C1/I1/S1/N1passes102markers/45connections/3saved boots/2reloads;no new timer/profile/version/client/kernel/live-read/install/artwork.'],
        limits=['Current startup/do43/45/hit41/do41/global RNG parity remains explicitly adapted,not recovered.','Effective-radius corridor/opaque hard-only slot treatment and lingering cloud groups follow the approved rule.','Special/unmapped/PvP/class-pet/property/graphical acceptance and full Enhanced Entropy remain incomplete;no class certified.','Future inspection dates qualify persistence only;effects/audio and talent/hotkey icons remain separately missing.'],inputs=inputs)
    target=R/'tools/reference/miasma-chain-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Chain learned receipt stale'
    else:target.write_text(encoded)
    print('Chain learned C1/I1/S1/N1:102markers/45connections/30actual inputs(9Chain)/2reloads;232handlers,Warlock23.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
