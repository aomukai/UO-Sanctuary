#!/usr/bin/env python3
"""Bind the complete proposed Slow Missiles rule and explicit producer limits."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/slow-missiles-rules';P='3c2b385aab4a9ac2d313d8b4bad0a4b8f3ffc581'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');now=snapshot(S/'source-snapshot-3.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-3.json');content=pin(S/'source-snapshot-2.json')['inputs']
    assert content['projectInputs']==now['projectInputs']and content['copiedData']==now['copiedData']
    assert {n for n in content['sourceInputs']if content['sourceInputs'][n]!=now['sourceInputs'][n]}=={'tests/Sanctuary.Integration/SlowMissilesRulesChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/abyss-cast/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/SlowMissilesRules.cs','tests/Sanctuary.Integration/SlowMissilesRulesChecks.cs'}and not a.keys()-b.keys()
    assert {n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/'+n+'.cs'for n in ('AmazonSlowMissiles','AmazonSlowMissilesCast','AmazonSlowMissilesCasting')}
    assert born['copiedData'].keys()-old['copiedData'].keys()=={'Data/Sanctuary/Balance/slow-missiles-native-targets.json'}and all(born['copiedData'][n]==h for n,h in old['copiedData'].items())
    for k,n in (('content',2),('integration',2)):
        p=S/f'{k}-build-{n}.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    failed=pin(S/'failed-run-1/birth-inputs.json');assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    base=pin('tools/reference/abyss-cast-native-evidence.json');expected=Counter(base['markers']);expected.update({'slow-missiles-rules-check':1,'slow-missiles-rules-check-result':1,'slow-missiles-rules-reload-check':2,'slow-missiles-rules-reload-check-result':2,'slow-missiles-windup-check':1,'slow-missiles-windup-reload-check':2})
    log=e.cohort(S,expected,177);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==78
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world','flame-wave-cast','abyss-world','abyss-cast','slow-missiles-rules','slow-missiles-windup'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    assert len(pin(S/'entropy-cloud-fixtures.json'))==3
    source=pin('tools/reference/slow-missiles-rules-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(R/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in ('tests/check_slow_missiles_rules.py','tools/record_slow_missiles_rules.py','docs/slow-missiles-rule-proposal.md','docs/slow-missiles-rules-checkpoint.md','docs/monster-combat-conversion-notes.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Complete proposed Slow Missiles native-target/current-producer rule',ruleApproved=False,learnedEnabled=False,handlers=235,completedClasses=[],contentBuild=2,integrationBuild=2,contentSourceSnapshot=2,sourceSnapshot=3,nativeRun=2,nativeMarkers=177,markers=dict(expected),authenticatedSessions=78,boots=3,reloads=2,saves=3,actualSlowMissilesInputs=0,retainedActualInputs=48,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Prepared SC/cancel/one paid current-rank release affects explicit native Lich and existing encounter targets without D2 identity injection;strict prepared policy remains unchanged.','Actual native firebolt endpoint/velocity capture,unchanged already-created packet,real lifetime/travel/HP impact,encounter payload and six-channel integer scaling with unchanged durations;CanSlow0retains damage reduction.','Stronger-state priority,atomic UTC clock validation and original recipient payload/creator/rank/expiry/identity/fractional resources through two actual reloads without unpaid-context replay.','Existing prepared Slow Missiles windup and all48Abyss/Flame Wave/Chain/Bolt/Mirrored inputs retain.'],
        limits=['Whole target/snapshot/packet-order rule and legacy instantaneous UO ranged/spell/breath conversion boundary require explicit approval;no learned Slow Missiles inputs yet.','Exact modern do6/second-stat/order/global RNG and unqualified child/burn/special/PvP/property/graphical scopes remain unverified;current native baseline geometry is an explicit adapter.','First run failed combined post-load firebolt travel/damage assertion with cause unassigned;I2tests full missile lifetime with failure diagnostics and unchanged gameplay DLL.','No class certification or final assets;four older Warlock proposals remain frozen.'],inputs=inputs)
    target=R/'tools/reference/slow-missiles-rules-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Slow Missiles rule C2/I2/S3/N2:177markers/78connections/48retained actual inputs/2reloads;whole decision pending,235handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
