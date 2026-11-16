#!/usr/bin/env python3
"""Record actual complete-item/untrained-grant baseline without changing readiness."""
import argparse
from collections import Counter
import subprocess,sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/boneshade-grant'
sys.path.insert(0,str(ROOT/'tests'))
from check_boneshade_grant import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    assert sum(r.values())==27
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/warlock-final-talents/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    changed={p for p,h in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=h}
    assert changed=={'tests/Sanctuary.Integration/BoneshadeGrantChecks.cs'}
    for p,h in born['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    assert {p for p,h in born['copiedData'].items()if h!=parent['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/named-skill-backlog.json'}
    for p,h in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==h,p
    # Both changed data files are previously committed reporting corrections.
    for p in ('data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/named-skill-backlog.json','data/Sanctuary/Balance/named-active-skills.json'):
        assert (ROOT/p).read_bytes()==subprocess.check_output(['git','show','a1445c6a:'+p],cwd=ROOT)
    assert len(e.load(ROOT/'data/Sanctuary/Balance/named-active-skills.json'))==16
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        text=(STAGE/(k.lower()+'-build-2.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),27);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==9 and 'denied by connection filter'not in log
    assert not any(' WRN]'in line and 'AccountPrompt'not in line for line in log.splitlines())
    for mode in ('right','quick'):assert log.count('boneshade-grant-'+mode+': windup')==3
    result=dict(component='Complete existing Boneshade untrained Bone Wall grant baseline',referenceVersion='3.3.93847',nativeMarkers=27,markers=dict(sorted(expected_markers().items())),connections=9,actualAbilityWireInputs=6,boots=3,saves=3,reloads=2,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,artifacts=born['artifacts'],changedSources=sorted(changed),gameplaySourceChanged=False,readinessRegistryChanged=False,newNamedItemsAdmitted=0,namedDirectGrantRegistry=16,nativeRebuilt=False,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Boneshade and Bone Wall were already enabled;this adds actual complete-item untrained cast/equip/trade/identity/broken/binding/save evidence,not a new admission. Ordinary weighted named generation is exercised;no kill/gamble economy or graphical proof is claimed. Two current data differences from the final-four cohort are earlier reporting-only commits. Other direct grants and proc/charge/aura systems remain separate;no new skill,source mechanics,assets or deployment.')
    files=[ROOT/'tools/record_boneshade_grant.py',ROOT/'docs/boneshade-grant-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'source-snapshot-2.json',STAGE/'native-run-2.log',STAGE/'content-build-2.log',STAGE/'integration-build-2.log',STAGE/'failed-run-1.tar.gz',*configuration()['extra_inputs']]
    e.write('boneshade-grant-native-evidence.json',result,files,check)
    print('Existing Boneshade grant:27results/6inputs/9connections/two reloads;16-entry registry and gameplay unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
