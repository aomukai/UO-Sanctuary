#!/usr/bin/env python3
"""Authenticate approved learned Miasma and automatic bolt/maker/cloud inputs."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/miasma-cast';PARENT='9799e350ccafa3d308a6b6b6e39816c519e022a6'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-2.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs']['sourceInputs']
    assert {n for n,h in first.items()if e.sha(ROOT/n)!=h}=={'tests/Sanctuary.Integration/MiasmaWorldChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    old=pin(ROOT/'.local/class-skills/miasma-world/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('CharacterProfiles','ClassDeath','MiasmaBoltCasting','MiasmaWorld','TimedEffects','WarlockCombat')}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('MiasmaLaunchChecks','MiasmaPacketChecks','MiasmaWorldChecks','WarlockCastClockChecks')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/MiasmaCastChecks.cs'}and not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}==changed
    assert born['copiedData'].keys()==old['copiedData'].keys()and{n for n in old['copiedData']if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind,num in (('content',1),('integration',2)):
        p=STAGE/f'{kind}-build-{num}.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for f in ('miasma-world','miasma-cast','mirrored-cast'):expected.update({f+'-check':1,f+'-check-result':1,f+'-reload-check':2,f+'-reload-check-result':2})
    labels=tuple('miasma-cast-'+s for s in ('left','right','quick'))+('mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick')
    for n in labels:expected.update({n+'-check':3,n+'-result':3})
    log=e.cohort(STAGE,expected,69);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==30
    for n in labels:assert log.count(n+': windup')==3
    assert log.count('Miasma learned:SC/FCR/current selected aim/all bindings,unarmed,one debit,no replay/cancel/low-mana fallback,respec snapshot,Stop retention,safe/class/death cleanup pass')==1
    assert log.count('Miasma learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay')==2
    for f in ('miasma-world','miasma-cast','mirrored-cast'):
        saved=pin(STAGE/(f+'-fixtures.json'));assert saved['Generation']==2
        if f=='miasma-cast':assert saved['Life']%256 and saved['Mana']%256
    failed=STAGE/'failed-native-1';bad=pin(failed/'birth-inputs.json');pin(failed/'birth-native-checks.log');pin(failed/'native-run-1.log')
    assert 'miasma-world-reload-check-result: failed'in(failed/'birth-native-checks.log').read_text()
    for n,h in bad['sourceInputs'].items():
        if n.endswith('.cs'):assert e.sha(failed/'source'/n)==h,n
    for k in ('content','integration'):assert e.sha(failed/k/f'Sanctuary.{k.title()}.dll')==bad['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    approval=pin('tools/reference/miasma-bolt-world-rule-approval.json');assert approval['approved']and approval['candidateCommit']==PARENT
    assert approval['questionItemId']=='["request_user_input_async","call_47510ce059d04003ac807757176c2a91",0]'and approval['answer']=='Approve the complete Miasma Bolt rule (Recommended)'
    assert e.sha(ROOT/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',PARENT+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending approval.**','**Approved by the user on2026-11-15 JST.** [Exact decision](../tools/reference/miasma-bolt-world-rule-approval.json). The body below is the original approved proposal;implementation/acceptance status is recorded separately.\n\n**Original proposal.**',1)==(ROOT/approval['proposal']).read_text();pin(approval['proposal'])
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(changed),'tests/Sanctuary.Integration/MiasmaCastChecks.cs','tests/check_miasma_cast.py','tools/record_miasma_cast.py','tools/build_skill_roadmap.py',
              'data/Sanctuary/Balance/class-skill-status.json','docs/miasma-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Miasma Bolt',approved=True,learnedEnabled=True,handlers=231,warlockHandlers=22,completedClasses=[],
        contentBuild=1,integrationBuild=2,contentSourceSnapshot=1,sourceSnapshot=2,nativeRun=2,nativeMarkers=69,markers=dict(expected),authenticatedSessions=30,boots=3,saves=3,reloads=2,
        actualInputs=21,actualMiasmaInputs=9,retainedActualMirroredInputs=12,miasmaInputCases={'left':3,'right':3,'quickUnarmed':3},graphicalInputVerified=False,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,artifacts=born['artifacts'],
        failedRuns=[dict(run=1,accepted=False,path=str(failed.relative_to(ROOT)),reason='Saved-owner fixture incorrectly required exact global NextId after unrelated learned/wire casts advanced it;now retains owner nodes/leases and requires a monotonic counter.',exactSourceArchivesVerified=True)],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},frozenPendingFiles=frozen,
        verified=['Complete rule approval recorded with exact original proposal,candidate and native receipt hashes;learned ordinary SC dispatch and existing shared world clock enabled.',
            'Nine actual left/right/quick-unarmed Miasma socket casts show unpaid windup,one mana debit,real automatic bolt damage,two makers,two clouds and a real recipient lease from cloud contacts. No manual tick drives these actors.',
            'Public SC/FCR/live selected-target aim,unarmed release,one payment/cancellation/no low-mana fallback,respec snapshot,Stop retention and actual safe/class/death cleanup pass.',
            'Original saved Witch/dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without transient replay. Prepared original bolt/maker/cloud graph and positive lease continue across two reloads.',
            'Prepared cloud damage/immune/potion/delay/expiry/gear snapshot/facet/offline/terrain/restore controls retain,along with12real Mirrored socket casts and automatic contacts.',
            'C1/I2/S2/N2passes69markers/30connections/3saved boots/2reloads. No client/kernel rebuild,new timer/profile/version,live reference read,installed deployment or artwork.'],
        limits=['Exact current hit41/do41/startup/magic getter parity remains explicitly adapted under approval.','Unmapped/special/PvP/class-pet and graphical/full-property acceptance remain unverified or refused;no class certified.',
            'Future inspection dates qualify saved graph continuation only;real-time socket actors are transient.','Enhanced Entropy remains partial until Chain/Abyss consumers;four older proposals remain pending. Final effects/audio and talent/hotkey icons remain separately missing.'],inputs=inputs)
    p=ROOT/'tools/reference/miasma-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Learned Miasma evidence stale'
    else:p.write_text(encoded)
    print('Miasma learned:C1/I2/S2/N2,69markers/30connections/9actual Miasma+12Mirrored casts/2reloads;231handlers,Warlock22.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
