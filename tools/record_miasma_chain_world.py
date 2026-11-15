#!/usr/bin/env python3
"""Authenticate the whole prepared Chain rule and retained shared consumers."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/miasma-chain-world';P='9553c2406daa5f2ee823cfe19265c22da13630c9'
def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');snapshot(S/'source-snapshot-13.json',S/'birth-inputs.json');pin(S/'source-snapshot-13.json')
    content=pin(S/'source-snapshot-10.json')['inputs']['sourceInputs'];assert {n for n,h in content.items()if e.sha(R/n)!=h}=={'tests/Sanctuary.Integration/MiasmaChainWorldChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(S/'birth-component/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    additions={'src/Sanctuary.Content/MiasmaChainWorld.cs','src/Sanctuary.Content/MiasmaChainCasting.cs','tests/Sanctuary.Integration/MiasmaChainWorldChecks.cs'}
    assert b.keys()-a.keys()==additions and not a.keys()-b.keys()and{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/MiasmaWorld.cs'}and born['copiedData']==old['copiedData']
    for k,num in (('content',7),('integration',8)):
        p=S/f'{k}-build-{num}.log';pin(p);assert all(x in p.read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/k/f'Sanctuary.{k.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    parent=pin('tools/reference/miasma-chain-birth-native-evidence.json');expected=Counter(parent['markers']);expected.update({'miasma-chain-world-check':1,'miasma-chain-world-check-result':1,'miasma-chain-world-reload-check':2,'miasma-chain-world-reload-check-result':2})
    log=e.cohort(S,expected,78);pin(S/'birth-native-checks.log');pin(S/'native-run-6.log');assert log.count('Client: 127.0.0.1: Connected.')==33
    for name in ('miasma-cast-left','miasma-cast-right','miasma-cast-quick','mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick'):assert log.count(name+': windup')==3
    for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world'):assert pin(S/(f+'-fixtures.json'))['Generation']==2
    saved=e.load(S/'miasma-chain-world-fixtures.json');owner=saved['Owner'];state=saved['State'];assert saved['Life']%256 and saved['Mana']%256
    assert len([n for n in state['Chains']if n['Owner']==owner])==1 and len([n for n in state['ChainMakers']if n['Owner']==owner])==1 and len([n for n in state['Nodes']if n['Owner']==owner])==2
    assert log.count('Chain world:original saved link/713maker/fixed clouds/positive lease/RNG/clocks/Witch/gear/FP256wounds+mana retain and resume through actual reload;legacy optional graph loads atomically;future inspection clock only')==2
    archives=[]
    for run,reason in ((3,'Lifecycle fixture expected a late call to backfill stamp fields;staged pumping fixed and inactive links aligned with parking.'),(4,'Restricted history fixture had worn gear/recent aggression;fixture combat/gear cleared without changing gameplay gates.'),(5,'Early reload fixture used maker deadline rather than earliest cloud/head/maker deadline.')):
        stage=S/f'failed-native-{run}';bad=pin(stage/'birth-inputs.json');pin(stage/'birth-native-checks.log');pin(stage/f'native-run-{run}.log');assert 'miasma-chain-world'in(stage/'birth-native-checks.log').read_text()and': failed'in(stage/'birth-native-checks.log').read_text()
        for n,h in bad['sourceInputs'].items():
            if n.endswith('.cs'):assert e.sha(stage/'source'/n)==h,n
        for k in ('content','integration'):assert e.sha(stage/k/f'Sanctuary.{k.title()}.dll')==bad['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
        archives.append(dict(run=run,accepted=False,reason=reason,exactSourceArchivesVerified=True))
    for n,h in old['sourceInputs'].items():
        if n.endswith('.cs'):assert e.sha(S/'birth-component/source'/n)==h,n
    for k in ('content','integration'):assert e.sha(S/'birth-component'/k/f'Sanctuary.{k.title()}.dll')==old['artifacts'][f'Assemblies/Sanctuary.{k.title()}.dll']
    source=pin('tools/reference/miasma-chain-world-source-review.json')
    for n,h in source['inputs'].items():assert e.sha(R/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in (*sorted(additions),'src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/MiasmaWorld.cs','tests/check_miasma_chain_world.py','tools/record_miasma_chain_world.py','docs/miasma-chain-world-rule-proposal.md','docs/miasma-chain-world-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Complete prepared Miasma Chain world',approved=False,chainLearnedEnabled=False,automaticChainClockEnabled=False,handlers=231,warlockHandlers=22,completedClasses=[],contentBuild=7,integrationBuild=8,contentSourceSnapshot=10,sourceSnapshot=13,nativeRun=6,nativeMarkers=78,markers=dict(expected),authenticatedSessions=33,boots=3,reloads=2,saves=3,actualChainInputs=0,retainedActualMiasmaInputs=9,retainedActualMirroredInputs=12,artifacts=born['artifacts'],preservation=e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],extendedExistingProfile=254,existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,failedNativeRuns=archives,frozenPendingFiles=frozen,
        verified=['Prepared SC/FCR/cancel/movement/weapon/late mana/once-only fractional release and actual711→712→713→696head/link/field graph pass.','Actual head/cloud damage,snapshot/gear/hard-effective ranks,half-second stamps/one-second per-chain versus independent delays/entrants and5/10paid FIFO caps including retained old fields pass.','Ten stamps/five-second link/half-open expiry,no historical stamp backfill,captured cloud Slow Missiles,post-link expiry and40source-unit teleport clipping pass.','Live/frozen dead endpoint,wall cut,same-facet teleport,inactive/facet/safe-area parking/resume/no reset and restricted class-history/death cleanup pass.','Explicit magic immunity and independent potion components,legacy optional JSON and atomic malformed/UTC/duplicate all-pool restore pass.','Original saved link/713maker/cloud geometry/lease/RNG/clocks/Witch/gear/fractional resources retain and resume through two reloads.21retained actual accepted Miasma Bolt/Mirrored casts protect shared services.'],
        limits=['One complete proposed adaptation requires approval;no Chain automatic clock or learned/socket/graphical claim.','Effective-radius corridor is sole damage width;opaque hard-only SrvCalc1is retained without an invented extra modifier.','Current do43/45/hit41/do41/startup/mastery/pierce/global RNG/world parity remains unavailable or explicitly adapted.','Ordinary supported NPC scope only;broader special/unmapped/PvP/class-pet/property/graphical acceptance and full Enhanced Entropy remain unfinished.','Future inspection dates qualify persistence only;effects/audio and talent/hotkey icons remain separately missing.'],inputs=inputs)
    target=R/'tools/reference/miasma-chain-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Chain whole-world receipt stale'
    else:target.write_text(encoded)
    print('Chain whole world C7/I8/S13/N6:78markers/33connections/2reloads,21retained real inputs;pending approval,231handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
