#!/usr/bin/env python3
"""Authenticate learned Abyss and all three Enhanced Entropy consumers."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/abyss-cast';P='81ec8f9d6e8989eaabb9910174bc3a3554c0d5c2'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');now=snapshot(S/'source-snapshot-2.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-2.json');content=pin(S/'source-snapshot-1.json')['inputs']
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    assert content['projectInputs']==now['projectInputs']and content['copiedData']==now['copiedData']
    assert {n for n in content['sourceInputs']if content['sourceInputs'][n]!=now['sourceInputs'][n]}=={'tests/Sanctuary.Integration/MiasmaWorldChecks.cs'}
    old=pin(R/'.local/class-skills/abyss-world/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('AbyssCasting','AbyssWorld','WarlockCombat')}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in ('AbyssWorldChecks','EntropyCloudChecks','MiasmaWorldChecks')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/AbyssCastChecks.cs'}and not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}==changed
    assert born['copiedData'].keys()==old['copiedData'].keys()and{n for n in old['copiedData']if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for k,n in (('content',1),('integration',2)):
        p=S/f'{k}-build-{n}.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    base=pin('tools/reference/abyss-world-native-evidence.json');expected=Counter(base['markers']);expected.update({'abyss-cast-check':1,'abyss-cast-check-result':1,'abyss-cast-reload-check':2,'abyss-cast-reload-check-result':2,'entropy-cloud-check':1,'entropy-cloud-reload-check':2})
    for mode in ('left','right','quick'):expected.update({'abyss-cast-'+mode+'-check':3,'abyss-cast-'+mode+'-result':3})
    log=e.cohort(S,expected,168);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==75
    for name in ('abyss-cast-left','abyss-cast-right','abyss-cast-quick','flame-wave-cast-left','flame-wave-cast-right','flame-wave-cast-quick','miasma-chain-cast-left','miasma-chain-cast-right','miasma-chain-cast-quick','miasma-cast-left','miasma-cast-right','miasma-cast-quick','mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick'):assert log.count(name+': windup')==3
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world','flame-wave-cast','abyss-world','abyss-cast'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    entropy=pin(S/'entropy-cloud-fixtures.json');assert len(entropy)==3 and all(n['Abyss']and n['Life']%256 and n['Mana']%256 for n in entropy)
    approval=pin('tools/reference/abyss-world-rule-approval.json');assert approval['approved']and approval['candidateCommit']==P and approval['questionItemId']=='["request_user_input_async","call_891209ac7329498e87b95fdd53242d25",0]'and approval['answer']=='Approve the complete Abyss rule (Recommended)'
    assert e.sha(R/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',P+':'+approval['proposal']],cwd=R);assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending approval; learned input and automatic world execution stay gated.**','**Approved by the user on 2026-11-16 JST.** [Exact decision](../tools/reference/abyss-world-rule-approval.json). The body below is the approved proposal; [current learned acceptance](abyss-cast-checkpoint.md) is recorded separately.',1)==(R/approval['proposal']).read_text();pin(approval['proposal'])
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    data=pin('data/Sanctuary/Balance/class-skill-status.json');assert sum(r['implementation']=='handler-present'for r in data['skills'])==235 and sum(r['implementation']=='handler-present'and r['characterClass']=='Warlock'for r in data['skills'])==26 and not data['completedClasses']
    for n in (*sorted(changed),'tests/Sanctuary.Integration/AbyssCastChecks.cs','tests/check_abyss_cast.py','tools/record_abyss_cast.py','tools/build_skill_roadmap.py','docs/abyss-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/skill-mechanical-reference.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Abyss and full Enhanced Entropy consumers',approved=True,abyssLearnedEnabled=True,automaticAbyssClockEnabled=True,enhancedEntropyEnabled=True,handlers=235,warlockHandlers=26,completedClasses=[],contentBuild=1,integrationBuild=2,contentSourceSnapshot=1,sourceSnapshot=2,nativeRun=2,nativeMarkers=168,markers=dict(expected),authenticatedSessions=75,boots=3,reloads=2,saves=3,actualInputs=48,actualAbyssInputs=9,actualInputBreakdown=dict(Abyss=9,FlameWave=9,MiasmaChain=9,MiasmaBolt=9,MirroredBlades=12),abyssInputCases=dict(left=3,right=3,quickUnarmed=3),graphicalInputVerified=False,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Complete Abyss adaptation approved with original proposal/candidate/native hashes;public ordinary SC/FCR joins existing entropy world clock.','Nine actual Abyss left/right/quick-unarmed socket casts show unpaid windup,one debit,automatic pulse HP loss,observed terminal and separate collapse HP loss;no manual world pump.','Public aim/unarmed/cancel/late mana/no fallback,respec/Stop,safe/class/death/FCR and saved Witch/dagger/fractional wounds+mana/bindings retain with fresh learned casts after two reloads.','All three Enhanced Entropy consumers qualify actual original-gear radius/duration thresholds,hard-only Chain data,passive binding refusal,fixed-parent-rank damage invariance and saved resources.','Prepared world/pull/terminal/root persistence/late/parking and all39retained Flame Wave/Chain/Bolt/Mirrored inputs pass.'],
        limits=['Current do39/global RNG/native geometry parity stays explicitly adapted;special/PvP/property/graphical/full-class scopes remain incomplete.','Four older Warlock proposals remain frozen pending;Amazon Slow Missiles remains another handler gap.','Effects/audio and talent/hotkey icons remain separate placeholders;future fixture dates qualify persistence only.','First learned run failed a stale retained fixture admission assertion;only that integration assertion changed before accepted I2/N2.'],inputs=inputs)
    target=R/'tools/reference/abyss-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Abyss/Enhanced Entropy C1/I2/S2/N2:168markers/75connections/48actual inputs(9Abyss)/2reloads;235handlers,Warlock26.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
