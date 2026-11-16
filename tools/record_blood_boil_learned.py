#!/usr/bin/env python3
"""Bind approved normal Blood Boil corpse input and original/bound persistence."""
import argparse
from collections import Counter
import json
import re
import subprocess
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/blood-boil-learned'
sys.path.insert(0,str(ROOT/'tests'))
from check_blood_boil_learned import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    assert sum(r.values())==39
    return r

def record(check=False):
    approval=e.load(ROOT/'tools/reference/warlock-final-talents-approval.json')
    assert approval['approved'] and 'warlock.bloodboil' in approval['approvedTalents']
    for p,h in approval['filesAtApprovalSha256'].items():assert e.sha(ROOT/p)==h,p
    born=e.load(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    parent=e.load(ROOT/'.local/class-skills/engorge-learned/birth-inputs.json')
    changed={p for p,h in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=h}
    assert changed=={'shared/ClassAbilities.cs','src/Sanctuary.Content/BloodBoilPrepared.cs','src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/BloodBoilWorldChecks.cs','tests/Sanctuary.Integration/BoundDemonRecipientChecks.cs','tests/Sanctuary.Integration/BloodBoilLearnedChecks.cs'}
    for p,h in born['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    assert {p for p,h in born['copiedData'].items() if h!=parent['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    old=json.loads(subprocess.check_output(['git','show','30f47235:data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));current=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert [a['talentId'] for a,b in zip(old['skills'],current['skills'],strict=True) if a!=b]==['warlock.bloodboil']
    assert old|{'skills':current['skills']}==current and sum(r['implementation']=='handler-present' for r in current['skills'])==239
    for p,h in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==h,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        build=(STAGE/(k.lower()+'-build-1.log')).read_text();assert all(v in build for v in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Approved=>true' in (ROOT/'src/Sanctuary.Content/BloodBoilPrepared.cs').read_text()
    log=e.cohort(STAGE,expected_markers(),39)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==15 and 'denied by connection filter' not in log
    for mode in ('left','right','quick'):assert log.count('blood-boil-learned-'+mode+': windup')==3
    files=[ROOT/'tools/record_blood_boil_learned.py',ROOT/'docs/blood-boil-learned-checkpoint.md',ROOT/'tools/reference/blood-boil-learned-client-evidence.json',ROOT/'tools/build_skill_roadmap.py',ROOT/'data/Sanctuary/Balance/class-skill-status.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'native-run-1.log',STAGE/'content-build-1.log',STAGE/'integration-build-1.log',*configuration()['extra_inputs']]
    result=dict(component='Learned Blood Boil original/bound self action',referenceVersion='3.3.93847',approved=True,defaultRuleEnabled=True,nativeMarkers=39,markers=dict(sorted(expected_markers().items())),connections=15,actualAbilityWireInputs=9,inputModes=['left','right','quick'],boots=3,saves=3,reloads=2,globalHandlers=239,warlockHandlers=29,remainingApprovedTalents=['warlock.consume'],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,artifacts=born['artifacts'],changedSources=sorted(changed),runtimeDataChange='Blood Boil handler/verification/remaining row only',nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.',log),protection=e.protection('tools/reference/blood-boil-learned-client-evidence.json',44),limits='Normal self packets and automatic release qualify three original species and a qualified native Daemon;single dying demon still explodes. Bound setup reuses its verified binder;no graphical purchase or whole-class certificate. Existing original/bound typed explosions,life payment,120ms delay and wound/order/RNG persistence remain scoped. Poison flag isolates exact resource checks from unrelated passive recovery. Current do160/special/PvP/property/graphical parity and final effects/icons remain open. No charms/runewords/current-client read/deployment.')
    e.write('blood-boil-learned-native-evidence.json',result,files,check)
    print('Blood Boil:39 native results/9 wire casts/15connections/two reloads;239global/29Warlock handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
