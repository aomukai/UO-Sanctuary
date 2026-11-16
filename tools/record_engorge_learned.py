#!/usr/bin/env python3
"""Bind approved normal Engorge corpse input and original/bound persistence."""
import argparse
from collections import Counter
import json
import re
import subprocess
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/engorge-learned'
sys.path.insert(0,str(ROOT/'tests'))
from check_engorge_learned import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    assert sum(r.values())==45
    return r

def record(check=False):
    approval=e.load(ROOT/'tools/reference/warlock-final-talents-approval.json')
    assert approval['approved'] and 'warlock.engorge' in approval['approvedTalents']
    for p,h in approval['filesAtApprovalSha256'].items():assert e.sha(ROOT/p)==h,p
    born=e.load(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    parent=e.load(ROOT/'.local/class-skills/blood-oath-learned/birth-inputs.json')
    changed={p for p,h in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/EngorgeEffects.cs','src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/EngorgeWorldChecks.cs','tests/Sanctuary.Integration/BoundDemonEngorgeChecks.cs','tests/Sanctuary.Integration/EngorgeLearnedChecks.cs'}
    for p,h in born['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    assert {p for p,h in born['copiedData'].items() if h!=parent['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    old=json.loads(subprocess.check_output(['git','show','a81d0e43:data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));current=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert [a['talentId'] for a,b in zip(old['skills'],current['skills'],strict=True) if a!=b]==['warlock.engorge']
    assert old|{'skills':current['skills']}==current and sum(r['implementation']=='handler-present' for r in current['skills'])==238
    for p,h in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==h,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        build=(STAGE/(k.lower()+'-build-2.log')).read_text();assert all(v in build for v in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Approved=>true' in (ROOT/'src/Sanctuary.Content/EngorgeEffects.cs').read_text()
    log=e.cohort(STAGE,expected_markers(),45)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==18 and 'denied by connection filter' not in log
    for mode in ('left','right','quick','ground'):assert log.count('engorge-learned-'+mode+': windup')==3
    files=[ROOT/'tools/record_engorge_learned.py',ROOT/'docs/engorge-learned-checkpoint.md',ROOT/'tools/build_skill_roadmap.py',ROOT/'data/Sanctuary/Balance/class-skill-status.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'native-run-2.log',STAGE/'content-build-2.log',STAGE/'integration-build-2.log',STAGE/'failed-run-1.tar.gz',*configuration()['extra_inputs']]
    result=dict(component='Learned Engorge original/bound corpse action',referenceVersion='3.3.93847',approved=True,defaultRuleEnabled=True,nativeMarkers=45,markers=dict(sorted(expected_markers().items())),connections=18,actualAbilityWireInputs=12,inputModes=['left','right','quick','ground quick'],boots=3,saves=3,reloads=2,globalHandlers=238,warlockHandlers=28,remainingApprovedTalents=['warlock.bloodboil','warlock.consume'],contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,artifacts=born['artifacts'],changedSources=sorted(changed),runtimeDataChange='Engorge handler/verification/remaining row only',nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.',log),protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),limits='Normal corpse packets and automatic timing/release qualify original three species and an explicitly qualified native Daemon. Bound setup reuses its verified binder;no graphical purchase or whole-class certificate. Existing original/bound combat,regen,leech,birth/loot/resources/expiry persistence remain scoped. Poison flag isolates direct healing from unrelated passive recovery in short wire fixtures;independent recovery cases retain. Current do161/special/PvP/property/graphical parity and final effects/icons remain open. No charms/runewords/current-client read/deployment.')
    e.write('engorge-learned-native-evidence.json',result,files,check)
    print('Engorge:45 native results/12 wire casts/18connections/two reloads;238global/28Warlock handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
