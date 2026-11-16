#!/usr/bin/env python3
"""Bind prepared eight-class player Fire Ball item emission and retained routes."""
import argparse,sys
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/player-item-fireball';BASE='48a0cd31'
sys.path.insert(0,str(ROOT/'tests'))
from check_player_item_fireball import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/sorceress-mastery-grants/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    compiled=e.load(STAGE/'source-snapshot-1.json')['inputs'];current=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert compiled['projectInputs']==current['projectInputs']
    assert all(current['sourceInputs'].get(p)==d for p,d in compiled['sourceInputs'].items() if p.startswith(('src/','shared/')))
    assert compiled['copiedData']==current['copiedData']==born['copiedData']==parent['copiedData']
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/NpcItemSpellOrigins.cs','src/Sanctuary.Content/PlayerItemFireBall.cs','src/Sanctuary.Content/SorceressSpellMissiles.cs','tests/Sanctuary.Integration/PlayerItemFireBallChecks.cs'],changed
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in [('Content',1),('Integration',2)]:
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),32)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==8 and 'denied by connection filter' not in log
    warnings=[line for line in log.splitlines() if 'Warning: Attempted to delete' in line]
    assert len(warnings)==1 and all('WardrobeSet' in line for line in warnings),warnings
    for n in configuration()['extra_target_input_commands']:assert log.count(n+': windup')==3
    saved=e.load(STAGE/'player-item-fireball-fixtures.json');assert len(saved)==8
    assert {s['Flight']['PlayerItemClass'] for s in saved}==set(range(101,109))
    for s in saved:
        f=s['Flight'];sorc=f['PlayerItemClass']==102
        assert s['Generation']==2 and s['Mana']==0 and f['Rank']==20
        # Independent pinned rank20 growth,then three hard Fire Bolt points,
        # then rank4 Fire Mastery. General +skills never changes proc rank20.
        def endpoint(maximum):
            value=28 if maximum else 12
            for level in range(2,21):value+=(13 if level<=8 else 23 if level<=16 else 28)+(2 if maximum else 0)
            value*=128
            if sorc:value=value*142//100;value+=value*51//100
            return value
        assert (f['Minimum256'],f['Maximum256'])==(endpoint(False),endpoint(True))
        assert f['Modifiers']['FireMastery']==(51 if sorc else 0)
        assert s['Carrier']['Properties']==[{'Packed':(195<<16)|(47<<6)|20,'Value':10}] and s['Carrier']['ItemBase']==[]
    files=[ROOT/'tools/record_player_item_fireball.py',ROOT/'docs/player-item-fireball-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tools/reference/npc-item-spell-origins-source-evidence.json',ROOT/'tools/reference/skill-execution-source-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'source-snapshot-1.json',STAGE/'source-snapshot-2.json',STAGE/'native-run-1.log',STAGE/'content-build-1.log',STAGE/'integration-build-2.log',STAGE/'failed-build-1.tar.gz',STAGE/'player-item-fireball-fixtures.json',*configuration()['extra_inputs']]
    result=dict(component='Prepared eight-class player item Fire Ball producer',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=32,markers=dict(sorted(expected_markers().items())),connections=8,actualLearnedWireInputs=6,preparedOriginCases=32,preparedAllocationRefusals=8,preparedOfflinePauses=8,savedPlayerCarriers=8,knownWardrobeSaveWarnings=1,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,gameplaySourceChanged=True,nativeRebuilt=False,persistenceVersionChanged=False,optionalV0JsonExtension='PlayerItemClass;absent on historical learned/NPC flights',installed=False,namedAdmissionChanged=False,automaticPlayerEventDispatch=False,wholeHellslayerAdmitted=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Prepared producer with mandatory allocation and an explicit event-only carrier;carrier snapshot lives in fixture manifest,not a full live Hellslayer GearState. Existing normal Fire Ball and Iron Golem producer regression controls pass. Protected item-effect frames,actual player event/RNG hosting,whole original projection and automatic golem registration remain required before named admission. No whole-class/graphical certificate,new reference read,imagegen or deployment.')
    e.write('player-item-fireball-native-evidence.json',result,files,check)
    print('Prepared player Fire Ball:32results/eight classes/eight saved carriers/six retained learned inputs/two reloads;all endpoints checked,Hellslayer still gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
