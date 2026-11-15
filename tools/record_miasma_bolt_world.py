#!/usr/bin/env python3
"""Bind the prepared Miasma candidate to exact native/build/save evidence."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/miasma-world';PARENT='ee40d5f7b8fde1417a062d72e0a2a0bcdc776ff6'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-6.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-6.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    old=pin(ROOT/'.local/class-skills/mirrored-cast/birth-inputs.json')
    a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    additions={'src/Sanctuary.Content/'+n+'.cs'for n in ('MiasmaBoltBirth','MiasmaBoltCasting','MiasmaWorld')}|{'tests/Sanctuary.Integration/MiasmaWorldChecks.cs'}
    assert b.keys()-a.keys()==additions and not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/ClassCombat.cs'}
    assert born['copiedData'].keys()-old['copiedData'].keys()=={'Data/Sanctuary/Balance/miasma-bolt-profile.json'}
    assert all(born['copiedData'][n]==h for n,h in old['copiedData'].items())
    for kind,num in (('content',4),('integration',4)):
        p=STAGE/f'{kind}-build-{num}.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for f in ('miasma-world','mirrored-cast'):expected.update({f+'-check':1,f+'-check-result':1,f+'-reload-check':2,f+'-reload-check-result':2})
    labels=('mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick')
    for n in labels:expected.update({n+'-check':3,n+'-result':3})
    log=e.cohort(STAGE,expected,45);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log');assert log.count('Client: 127.0.0.1: Connected.')==18
    for n in labels:assert log.count(n+': windup')==3
    saved=pin(STAGE/'miasma-world-fixtures.json');mirror=pin(STAGE/'mirrored-cast-fixtures.json')
    assert saved['Generation']==mirror['Generation']==2 and saved['Life']%256 and saved['Mana']%256
    nodes=saved['State']['Nodes'];assert Counter(n['Kind']for n in nodes)==Counter({0:1,1:2,2:2})and saved['State']['Leases']
    assert log.count('Miasma world:original simultaneous bolt/makers/clouds,positive lease,RNG/birth/shape/clocks,Witch/gear/FP256wounds+mana retained and continued through actual reload;future inspection clock only')==2
    archive=[]
    for run in (1,2,3):
        stage=STAGE/f'failed-native-{run}';bad=pin(stage/'birth-inputs.json');pin(stage/'birth-native-checks.log');pin(stage/f'native-run-{run}.log')
        assert 'miasma-world-check-result: failed'in(stage/'birth-native-checks.log').read_text()
        for n,h in bad['sourceInputs'].items():
            if n.endswith('.cs'):assert e.sha(stage/'source'/n)==h,n
        for kind in ('content','integration'):
            assert e.sha(stage/kind/f'Sanctuary.{kind.title()}.dll')==bad['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
        archive.append(dict(run=run,path=str(stage.relative_to(ROOT)),accepted=False,exactSourceArchivesVerified=True))
    source=pin('tools/reference/miasma-bolt-world-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(ROOT/n)==h,n
    for n in sorted(additions|{'src/Sanctuary.Content/ClassCombat.cs'}):pin(n)
    for n in ('data/Sanctuary/Balance/miasma-bolt-profile.json','tests/check_miasma_world.py','tools/record_miasma_bolt_world.py',
              'docs/miasma-bolt-world-rule-proposal.md','docs/miasma-bolt-world-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
              'docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    result=dict(referenceVersion='3.3.93847',component='Complete prepared Miasma Bolt world',approved=False,learnedEnabled=False,handlers=230,warlockHandlers=21,completedClasses=[],
        contentBuild=4,integrationBuild=4,sourceSnapshot=6,nativeRun=4,nativeMarkers=45,markers=dict(expected),authenticatedSessions=18,boots=3,saves=3,reloads=2,
        actualMiasmaInputs=0,retainedActualMirroredInputs=12,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[254],existingSerializationVersionsChanged=False,
        newLiveReferenceReads=0,artifacts=born['artifacts'],failedRuns=archive,frozenPendingFiles=frozen,
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44), 'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=['Prepared ordinary SC cancellation,movement,weapon change,late mana,one fractional release/no replay.','Real697bolt contact,two736makers and two705magic clouds;captured Entropy and damage survive gear/training changes.',
            'One-second shared within-cast lease,separate-cast stacking,new entrants,no expiry damage;explicit magic immunity and independent potion components.',
            'Range/wall cloud creation including corrected half-subtile/world-tile endpoint;offline/facet suppression,resume,absolute lifetime and atomic malformed-state refusal.',
            'Original simultaneous bolt/maker/cloud graph,RNG/clocks,positive lease,Witch/equipment/fractional life and mana retain and actually continue through two reloads.',
            'Twelve retained real four-kind Mirrored socket casts and automatic contacts;shared ClassCombat transient hooks retain approved learned routes.'],
        limits=['One complete proposed hit41/do41/action/world policy needs approval;automatic clock and learned Miasma remain gated.','Cloud circle/layout,first/last contacts,primary double contact,delay identity,mastery/pierce/event/lifecycle are explicit adaptations,not recovered current source.',
            'No graphical playtesting or full special/unmapped/PvP/class-pet acceptance;future inspection dates qualify persistence only.','Full Enhanced Entropy remains partial until Chain/Abyss consumers. Effects/audio and tree/hotkey icons remain separately missing.'],inputs=inputs)
    p=ROOT/'tools/reference/miasma-bolt-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded,'Miasma world evidence stale'
    else:p.write_text(encoded)
    print('Miasma prepared:C4/I4/S6/N4,45markers/18connections/2reloads;12retained Mirrored inputs;learned gated,230handlers/Warlock21.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
