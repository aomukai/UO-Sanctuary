#!/usr/bin/env python3
"""Bind the complete prepared Flame Wave candidate to its native world/reloads."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/flame-wave-world';P='baab0c64f0fecd1f0cd87f904ef1e1d3de1b276a'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-3.json',S/'birth-inputs.json');pin(S/'source-snapshot-3.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    base=pin('tools/reference/flame-wave-birth-native-evidence.json');old=pin(S/'birth-component/birth-inputs.json')
    assert born['copiedData']==old['copiedData']
    for k in ('content','integration'):
        p=S/f'{k}-build-3.log';pin(p);assert all(t in p.read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    expected=Counter(base['markers']);expected.update({'flame-wave-world-check':1,'flame-wave-world-check-result':1,'flame-wave-world-reload-check':2,'flame-wave-world-reload-check-result':2})
    log=e.cohort(S,expected,111);pin(S/'birth-native-checks.log');pin(S/'native-run-3.log');assert log.count('Client: 127.0.0.1: Connected.')==48
    assert 'SC/FCR interruption/late mana/one fractional payment' in log and log.count('Flame Wave world:saved moving lanes')==2
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    source=pin('tools/reference/flame-wave-world-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(R/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in ('src/Sanctuary.Content/FlameWaveWorld.cs','src/Sanctuary.Content/FlameWaveCasting.cs','tests/Sanctuary.Integration/FlameWaveWorldChecks.cs','tests/check_flame_wave_world.py','tools/record_flame_wave_world.py','docs/flame-wave-world-rule-proposal.md','docs/flame-wave-world-checkpoint.md','docs/skill-effects.md','docs/skill-icons.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Complete prepared Flame Wave world/action candidate',flameWaveWorldImplemented=True,ruleApproved=False,learnedEnabled=False,automaticClockEnabled=False,handlers=232,warlockHandlers=23,completedClasses=[],contentBuild=3,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeMarkers=111,markers=dict(expected),authenticatedSessions=48,boots=3,reloads=2,saves=3,actualFlameWaveInputs=0,retainedActualInputs=30,retainedActualInputBreakdown=dict(MiasmaChain=9,MiasmaBolt=9,MirroredBlades=12),artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[dict(name='SanctuaryFlameWave',id=255,version=0)],clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Prepared SC/FCR unpaid windup,early/cancel/movement/weapon/late mana,one fractional release and15-frame cooldown.','Actual reveal/travelling lanes,paired tail/static→240 fields,head+ground damage,overlap positive control,independent casts,wall cuts,explicit fire immunity and immutable gear capture.','Offline/facet/safe-area suspension,no reset,expiry/death cleanup,atomic graph admission and two actual reloads with original gear/wounds/mana/RNG/clocks;overdue load skips offline frames.','All retained Chain/Bolt/Mirrored30real socket casts and existing resource/item/binding regression remain passing.'],
        limits=['Complete rule requires explicit adaptation approval;no actual learned Flame Wave socket input yet.','Modern156/42/38composition remains unavailable;proposal explicitly defines reveal/spread/delay,tail/child inheritance/overlap andworld/input/lifecycle edges.','Ordinary supported NPC scope;no all-special/PvP/property/class or final-asset certificate.'],inputs=inputs)
    target=R/'tools/reference/flame-wave-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Prepared Flame Wave C3/I3/S3/N3:111markers/48connections/30retained actual inputs/2reloads;complete rule pending,learned gated,232handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
