#!/usr/bin/env python3
"""Bind approved Slow Missiles input, projectile and persistence acceptance."""
import argparse, hashlib, json, subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

R=e.ROOT
S=R/'.local/class-skills/slow-missiles-learned'
P='c3eff2688f0394f59d84039fe0277d2621c362bd'

def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str) else p
        inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p) if p.suffix=='.json' else None
    born=pin(S/'birth-inputs.json')
    now=snapshot(S/'source-snapshot-5.json',S/'birth-inputs.json')['inputs']
    pin(S/'source-snapshot-5.json')
    content=pin(S/'source-snapshot-1.json')['inputs']
    assert content['projectInputs']==now['projectInputs'] and content['copiedData']==now['copiedData']
    assert {n for n in content['sourceInputs'] if content['sourceInputs'][n]!=now['sourceInputs'][n]}=={
        'tests/Sanctuary.Integration/SlowMissilesLearnedChecks.cs','tests/Sanctuary.Integration/SlowMissilesRulesChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/slow-missiles-rules/birth-inputs.json')
    a={n:h for n,h in old['sourceInputs'].items() if n.endswith('.cs')}
    b={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    changed={'shared/ClassAbilities.cs'}|{'src/Sanctuary.Content/'+n+'.cs' for n in ('AmazonCombat','AmazonSlowMissilesCasting','SlowMissilesRules')}|{
        'tests/Sanctuary.Integration/'+n+'.cs' for n in ('SlowMissilesRulesChecks','SlowMissilesWindupChecks','SlowMissilesCastChecks','SlowMissilesComponentChecks')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/SlowMissilesLearnedChecks.cs'} and not a.keys()-b.keys()
    assert {n for n in a if a[n]!=b[n]}==changed
    assert born['copiedData'].keys()==old['copiedData'].keys()
    assert {n for n in old['copiedData'] if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind,number in (('content',1),('integration',5)):
        p=S/f'{kind}-build-{number}.log';pin(p)
        assert all(x in p.read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    failed_runs=[]
    for run in range(1,5):
        folder=S/f'failed-run-{run}'
        failed=pin(folder/'birth-inputs.json');pin(folder/'birth-native-checks.log')
        assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        failed_runs.append(str(folder.relative_to(R)))
    base=pin('tools/reference/slow-missiles-rules-native-evidence.json')
    expected=Counter(base['markers'])
    expected.update({'slow-missiles-learned-check':1,'slow-missiles-learned-check-result':1,
        'slow-missiles-learned-reload-check':2,'slow-missiles-learned-reload-check-result':2})
    for mode in ('right','quick','fcr-quick'):
        expected.update({f'slow-missiles-learned-{mode}-check':3,f'slow-missiles-learned-{mode}-result':3})
    log=e.cohort(S,expected,201);pin(S/'birth-native-checks.log');pin(S/'native-run-5.log')
    assert log.count('Client: 127.0.0.1: Connected.')==90
    modes=('slow-missiles-learned-right','slow-missiles-learned-quick','slow-missiles-learned-fcr-quick',
        'abyss-cast-left','abyss-cast-right','abyss-cast-quick','flame-wave-cast-left','flame-wave-cast-right','flame-wave-cast-quick',
        'miasma-chain-cast-left','miasma-chain-cast-right','miasma-chain-cast-quick','miasma-cast-left','miasma-cast-right',
        'miasma-cast-quick','mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick')
    assert len(modes)==19
    for name in modes:assert log.count(name+': windup')==3,name
    for family in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast',
        'flame-wave-world','flame-wave-cast','abyss-world','abyss-cast','slow-missiles-rules','slow-missiles-windup','slow-missiles-learned'):
        assert pin(S/(family+'-fixtures.json'))['Generation']==2
    approval=pin('tools/reference/slow-missiles-rule-approval.json')
    assert approval['approved'] and approval['candidateCommit']==P
    assert approval['questionItemId']=='["request_user_input_async","call_85022e694c8143f1a4ce280db6600369",0]'
    assert approval['answer']=='Approve complete rule (Recommended)'
    assert e.sha(R/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',P+':'+approval['proposal']],cwd=R)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    assert original.decode().replace('**Pending approval; public learned input stays gated.**',
        '**Approved by the user on 2026-11-16 JST.** [Exact decision](../tools/reference/slow-missiles-rule-approval.json). The body below is the approved proposal; learned-input acceptance is recorded separately.',1)==(R/approval['proposal']).read_text()
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    status=pin('data/Sanctuary/Balance/class-skill-status.json')
    assert sum(n['implementation']=='handler-present' for n in status['skills'])==236 and not status['completedClasses']
    assert sum(n['implementation']=='handler-present' and n['characterClass']=='Amazon' for n in status['skills'])==30
    for n in (*sorted(changed),'tests/Sanctuary.Integration/SlowMissilesLearnedChecks.cs','tests/check_slow_missiles_learned.py',
        'tools/record_slow_missiles_learned.py','tools/build_skill_roadmap.py','tools/reference/slow-missiles-learned-client-evidence.json',
        approval['proposal'],'docs/monster-combat-conversion-notes.md','docs/slow-missiles-learned-checkpoint.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Slow Missiles',approved=True,learnedEnabled=True,
        handlers=236,amazonHandlers=30,warlockHandlers=26,completedClasses=[],contentBuild=1,integrationBuild=5,
        contentSourceSnapshot=1,sourceSnapshot=5,nativeRun=5,nativeMarkers=201,markers=dict(expected),authenticatedSessions=90,
        boots=3,reloads=2,saves=3,actualInputs=57,actualSlowMissilesInputs=9,
        actualInputBreakdown=dict(SlowMissiles=9,Abyss=9,FlameWave=9,MiasmaChain=9,MiasmaBolt=9,MirroredBlades=12),
        slowMissilesInputCases=dict(rightDagger=3,quickUnarmed=3,quickMagefistFcr=3),graphicalInputVerified=False,
        artifacts=born['artifacts'],preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),
        newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,clientRebuilt=True,nativeRebuilt=False,
        newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        failedRuns=failed_runs,
        verified=['Complete approved action/state/native-target/current-producer rule enabled through learned right and quick self casts.',
            'Nine actual socket inputs verify dagger,unarmed and Magefist FCR clocks,unpaid windup,one mana debit and automatic slowed firebolt HP impact.',
            'Cancellation,late mana,respec,Stop,class change and caster death retain the approved state and admission behavior.',
            'Original Amazon,dagger,recipient state,UTC expiry,fractional life/mana and right/quick bindings retain through two actual reloads;fresh casts work without replay.',
            'All48retained accepted inputs and prepared native/encounter speed/damage/priority/UTC controls pass.'],
        limits=['No class certification or graphical playtest;effects/audio and talent/hotkey icons remain identified placeholders.',
            'Legacy UO instantaneous ranged hits and spell/breath timers remain on the explicitly approved later monster-conversion track;not claimed slowed.',
            'Current protected D2R ordering and native classification/geometry remain approved adaptations;special producers,PvP and broader property scopes remain unqualified.',
            'Four older Warlock proposals remain frozen pending.'],inputs=inputs)
    path=R/'tools/reference/slow-missiles-learned-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Slow Missiles C1/I5/S5/N5:201markers/90connections/57actual inputs(9Slow)/2reloads;236handlers,Amazon30.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
