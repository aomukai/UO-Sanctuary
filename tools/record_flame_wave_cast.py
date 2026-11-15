#!/usr/bin/env python3
"""Authenticate approved Flame Wave learned input and automatic front/burn damage."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/flame-wave-cast';P='14806efd73cd5dfcf3172e1f398e8902d78c80d0'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');now=snapshot(S/'source-snapshot-3.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-3.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    content=pin(S/'source-snapshot-1.json')['inputs'];integration=pin(S/'source-snapshot-2.json')['inputs']
    # Only fixture training keys changed after C1. JSON is copied at native boot,
    # separately from compilation; S3 pins the regenerated runtime status data.
    assert {n:h for n,h in content['sourceInputs'].items()if not n.startswith('tests/')}=={n:h for n,h in now['sourceInputs'].items()if not n.startswith('tests/')}
    assert integration['sourceInputs']==now['sourceInputs']and content['projectInputs']==integration['projectInputs']==now['projectInputs']
    assert {n for n in content['sourceInputs']if content['sourceInputs'][n]!=now['sourceInputs'][n]}=={'tests/Sanctuary.Integration/FlameWaveCastChecks.cs'}
    old=pin(R/'.local/class-skills/flame-wave-world/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('FlameWaveCasting','FlameWaveWorld','WarlockCombat')}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in ('FlameWaveBirthChecks','FlameWaveWorldChecks')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/FlameWaveCastChecks.cs'}and not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}==changed
    assert born['copiedData'].keys()==old['copiedData'].keys()and{n for n in old['copiedData']if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for k,build in (('content',1),('integration',2)):
        p=S/f'{k}-build-{build}.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    parent=pin('tools/reference/flame-wave-world-native-evidence.json');expected=Counter(parent['markers']);expected.update({'flame-wave-cast-check':1,'flame-wave-cast-check-result':1,'flame-wave-cast-reload-check':2,'flame-wave-cast-reload-check-result':2})
    for mode in ('left','right','quick'):expected.update({'flame-wave-cast-'+mode+'-check':3,'flame-wave-cast-'+mode+'-result':3})
    log=e.cohort(S,expected,135);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==60
    for name in ('flame-wave-cast-left','flame-wave-cast-right','flame-wave-cast-quick','miasma-chain-cast-left','miasma-chain-cast-right','miasma-chain-cast-quick','miasma-cast-left','miasma-cast-right','miasma-cast-quick','mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick'):assert log.count(name+': windup')==3
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world','flame-wave-cast'):
        saved=pin(S/(f+'-fixtures.json'));assert saved['Generation']==2
        if f=='flame-wave-cast':assert saved['Life']%256 and saved['Mana']%256
    approval=pin('tools/reference/flame-wave-world-rule-approval.json');assert approval['approved']and approval['candidateCommit']==P
    assert approval['questionItemId']=='["request_user_input_async","call_b2bed4a4057b4abf8405aa2ec98415f6",0]'and approval['answer']=='Approve the complete Flame Wave rule (Recommended)'
    assert e.sha(R/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',P+':'+approval['proposal']],cwd=R);assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending approval; learned input and automatic progression stay gated.**','**Approved by the user on 2026-11-15 JST.** [Exact decision](../tools/reference/flame-wave-world-rule-approval.json). The body below records the approved proposal; current learned acceptance is in [the checkpoint](flame-wave-cast-checkpoint.md).',1)==(R/approval['proposal']).read_text();pin(approval['proposal'])
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    data=pin('data/Sanctuary/Balance/class-skill-status.json');assert sum(r['implementation']=='handler-present'for r in data['skills'])==233 and sum(r['implementation']=='handler-present'and r['characterClass']=='Warlock'for r in data['skills'])==24 and not data['completedClasses']
    for n in (*sorted(changed),'tests/Sanctuary.Integration/FlameWaveCastChecks.cs','tests/check_flame_wave_cast.py','tools/record_flame_wave_cast.py','tools/build_skill_roadmap.py','docs/flame-wave-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/skill-mechanical-reference.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Flame Wave',approved=True,flameWaveLearnedEnabled=True,automaticFlameWaveClockEnabled=True,handlers=233,warlockHandlers=24,completedClasses=[],contentBuild=1,integrationBuild=2,contentSourceSnapshot=1,integrationSourceSnapshot=2,sourceSnapshot=3,nativeRun=2,nativeMarkers=135,markers=dict(expected),authenticatedSessions=60,boots=3,reloads=2,saves=3,actualInputs=39,actualFlameWaveInputs=9,actualInputBreakdown=dict(FlameWave=9,MiasmaChain=9,MiasmaBolt=9,MirroredBlades=12),flameWaveInputCases=dict(left=3,right=3,quickUnarmed=3),graphicalInputVerified=False,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Complete rule approved with immutable original proposal/candidate/native hashes;public SC/FCR uses existing ordinary Warlock timing and world hook.','Nine actual Flame Wave left/right/quick-unarmed socket casts show unpaid windup,one fractional debit and automatic front then ground damage;no manual world pump.','Public moving-target aim/unarmed/cancel/late mana/no fallback,respec/Stop snapshots,FCR versus weapon/IAS/chill,safe-area and actual class/death cleanup pass.','Original saved Witch/dagger/fractional wounds+mana/all bindings retain and fresh learned input works after two actual reloads without transient replay.','Prepared world graph/fire/overlap/terrain/immunity/parking/atomic restore/overdue-load skip plus prior30accepted Chain/Bolt/Mirrored inputs retain.'],
        limits=['Unavailable modern156/42/38composition remains explicitly adapted,not recovered.','Special/unmapped/PvP/property/graphical acceptance and full Enhanced Entropy/Abyss remain incomplete;no class certified.','Final effects/audio and talent/hotkey icons remain separate placeholders;future fixture dates qualify persistence only.','Failed first learned run is archived:fixture trained Chain instead of Flame Wave;only integration training keys changed before accepted I2/N2.'],inputs=inputs)
    target=R/'tools/reference/flame-wave-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Flame Wave learned receipt stale'
    else:target.write_text(encoded)
    print('Flame Wave learned C1/I2/S3/N2:135markers/60connections/39actual inputs(9Flame)/2reloads;233handlers,Warlock24.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
