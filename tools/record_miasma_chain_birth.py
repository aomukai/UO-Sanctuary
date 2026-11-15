#!/usr/bin/env python3
"""Record scoped Chain birth/len proof without claiming a Chain world handler."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/miasma-chain-world';P='160833168fa2ed92e41bebb8001b452da65d78d7'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-3.json',S/'birth-inputs.json');pin(S/'source-snapshot-3.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/miasma-cast/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/MiasmaChainBirth.cs','tests/Sanctuary.Integration/MiasmaChainBirthChecks.cs'}and not a.keys()-b.keys()
    assert {n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/SkillMechanics.cs','src/Sanctuary.Content/MiasmaChainLaunch.cs'}and born['copiedData']==old['copiedData']
    for k in ('content','integration'):
        p=S/f'{k}-build-2.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    parent=pin('tools/reference/miasma-cast-native-evidence.json');expected=Counter(parent['markers']);expected.update({'miasma-chain-birth-check':3});log=e.cohort(S,expected,72)
    pin(S/'birth-native-checks.log');pin(S/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==30
    for f in ('miasma-world','miasma-cast','mirrored-cast'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    bad=S/'failed-native-1';prior=pin(bad/'birth-inputs.json');pin(bad/'birth-native-checks.log');pin(bad/'native-run-1.log')
    assert "key 'miasmachains'"in(bad/'birth-native-checks.log').read_text()
    for n,h in prior['sourceInputs'].items():
        if n.endswith('.cs'):assert e.sha(bad/'source'/n)==h,n
    for k in ('content','integration'):assert e.sha(bad/k/f'Sanctuary.{k.title()}.dll')==prior['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    source=pin('tools/reference/miasma-chain-birth-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(R/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in ('src/Sanctuary.Content/MiasmaChainBirth.cs','src/Sanctuary.Content/MiasmaChainLaunch.cs','src/Sanctuary.Content/SkillMechanics.cs','tests/Sanctuary.Integration/MiasmaChainBirthChecks.cs','tests/check_miasma_chain_world.py','tools/record_miasma_chain_birth.py','docs/miasma-chain-birth-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Chain captured suppliers and registered len callback',chainLearnedEnabled=False,chainWorldImplemented=False,handlers=231,warlockHandlers=22,completedClasses=[],contentBuild=2,integrationBuild=2,sourceSnapshot=3,nativeRun=2,nativeMarkers=72,markers=dict(expected),authenticatedSessions=30,boots=3,reloads=2,saves=3,retainedActualMiasmaInputs=9,retainedActualMirroredInputs=12,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,failedNativeRuns=[dict(run=1,reason='Isolated Chain rows missing from general catalogue;corrected to owned route lookup.',accepted=False,exactSourceArchivesVerified=True)],frozenPendingFiles=frozen,verified=['Chain rank1/20 numeric goldens,cap boundaries,125-frame link,aura/poison duration separation and actual gear/effective/hard Entropy snapshots pass.','Accepted Miasma Bolt/Mirrored world/lifecycle/socket/save cohorts retain across two reloads.'],limits=['No Chain motion/attachment/cloud corridor or world action/save handler yet.','No modern caller/rate/do43/45/hit41/do41 parity or new adaptation approval.','Full Enhanced Entropy and final assets remain unfinished.'],inputs=inputs)
    target=R/'tools/reference/miasma-chain-birth-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Chain suppliers C2/I2/S3/N2:72markers,21retained actual inputs,2reloads;world/learned remain gated,231handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
