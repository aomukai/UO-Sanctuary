#!/usr/bin/env python3
"""Bind the complete prepared Abyss candidate and shared entropy-root regression."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/abyss-world';P='390805571ceb12831c499fdcd3f13b16330a62a0'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-4.json',S/'birth-inputs.json');pin(S/'source-snapshot-4.json')
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    base=pin('tools/reference/flame-wave-cast-native-evidence.json');old=pin(R/'.local/class-skills/flame-wave-cast/birth-inputs.json')
    a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/'+n+'.cs'for n in ('AbyssBirth','AbyssWorld','AbyssCasting')}|{'tests/Sanctuary.Integration/AbyssWorldChecks.cs'}
    assert not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/'+n+'.cs'for n in ('ClassCombat','MiasmaWorld','TimedEffects')}
    assert born['copiedData'].keys()-old['copiedData'].keys()=={'Data/Sanctuary/Balance/abyss-route.json'}and{n for n in old['copiedData']if born['copiedData'][n]!=old['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for k,n in (('content',3),('integration',2)):
        p=S/f'{k}-build-{n}.log';pin(p);assert all(t in p.read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    expected=Counter(base['markers']);expected.update({'abyss-world-check':1,'abyss-world-check-result':1,'abyss-world-reload-check':2,'abyss-world-reload-check-result':2})
    log=e.cohort(S,expected,141);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log');assert log.count('Client: 127.0.0.1: Connected.')==63
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world','flame-wave-cast','abyss-world'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    source=pin('tools/reference/abyss-world-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(R/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    data=pin('data/Sanctuary/Balance/class-skill-status.json');assert sum(r['implementation']=='handler-present'for r in data['skills'])==233 and not data['completedClasses']
    for n in ('tests/Sanctuary.Integration/AbyssWorldChecks.cs','tests/check_abyss_world.py','tools/record_abyss_world.py','tools/build_skill_roadmap.py','docs/abyss-world-rule-proposal.md','docs/abyss-world-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Complete prepared Abyss world/action candidate',abyssWorldImplemented=True,ruleApproved=False,learnedEnabled=False,automaticClockEnabled=False,handlers=233,warlockHandlers=24,completedClasses=[],contentBuild=3,integrationBuild=2,sourceSnapshot=4,nativeRun=2,nativeMarkers=141,markers=dict(expected),authenticatedSessions=63,boots=3,reloads=2,saves=3,actualAbyssInputs=0,retainedActualInputs=39,retainedActualInputBreakdown=dict(FlameWave=9,MiasmaChain=9,MiasmaBolt=9,MirroredBlades=12),artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,existingProfileOptionalFields=['SanctuaryMiasmaWorld.Abysses'],clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Owned graph,numeric/radius/synergy/cost goldens and actual original gear threshold;general sksrc remains unsupported outside scoped candidate binding.','Prepared SC/FCR windup/interrupt/late mana/one payment/cooldown and qualified shared ground launcher.','Actual center pulse/terminal HP damage,real pull,frozen/vertical-center/elite exclusions,wall/ground refusal,independent casts,magic immunity and late/no-replay windows.','Offline/facet/town parking,remaining lifetime/death cleanup,atomic cross-pool/legacy JSON and original simultaneous center+terminal,RNG,gear/Witch/wounds/mana through two actual reloads;overdue loaded stage skips.','All39retained Flame Wave/Chain/Bolt/Mirrored socket casts and shared item/resource/binding/world consumers pass.'],
        limits=['Whole modern do39composition/sksrc/pull/terminal/world rule requires explicit approval before learned/automatic admission.','Pinned cooldown table30versus tooltip50and terminal71510–15versus skill tooltip20–40remain explicit proposed choices,not resolved source claims.','No Abyss actual socket/graphical acceptance or full Enhanced Entropy/class/special/PvP/property certificate;final assets remain separate.'],inputs=inputs)
    target=R/'tools/reference/abyss-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Prepared Abyss C3/I2/S4/N2:141markers/63connections/39retained actual inputs/2reloads;complete rule pending,233handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
