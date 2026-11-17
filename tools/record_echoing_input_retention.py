#!/usr/bin/env python3
"""Record real Hex source-input inspection without whole-Warlock claims."""
import argparse,hashlib,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/echoing-input-retention';FULL=ROOT/'.local/class-skills/warlock-lifecycle-acceptance'
def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/druid-lifecycle-acceptance/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');assert born['copiedData']==parent['copiedData']
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    echo='tests/Sanctuary.Integration/EchoingStrikeInputChecks.cs';bind='tests/Sanctuary.Integration/BindDemonLearnedChecks.cs'
    assert sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)==[bind,echo]
    with tarfile.open(FULL/'failed-native-2.tar.gz')as a:
        old=a.extractfile(echo).read().decode();assert hashlib.sha256(old.encode()).hexdigest()==parent['sourceInputs'][echo]
        current=(ROOT/echo).read_text();start=current.index('            // Retain the real captured Hex');end=current.index('            rows[index++]',start);assert current[:start]+current[end:]==old
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'echoing-strike-inputs-check':1,'echoing-strike-inputs-reload-check':2})
    log=e.cohort(STAGE,expected,12);assert 'Warning: Attempted to delete'not in log
    assert all(t in(FULL/'integration-build-2.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    paths=['tools/record_echoing_input_retention.py','tests/check_echoing_input_retention.py','docs/warlock-lifecycle-acceptance-checkpoint.md','docs/class-skill-roadmap.md',bind,echo]
    files=[*(ROOT/p for p in paths),*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',FULL/'failed-native-2.tar.gz',FULL/'integration-build-2.log',FULL/'source-snapshot-3.json']
    result=dict(component='Echoing original Warlock/Witch source-input inspection retains real Hex and saved resources',referenceVersion='3.3.93847',nativeMarkers=12,markers=dict(sorted(expected.items())),boots=3,saves=3,reloads=2,wireInputs=0,artifacts=born['artifacts'],productionCodeChanged=False,runtimeDataChanged=False,integrationBuild=2,classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Original two-gender unique-weapon,helmet,poison equipment,hard/soft-rank,immutable source and fractional-resource assertions retain. Inspection extends only the real Bane deadline;capture,life,mana,durability,other buffs/debuffs are explicitly unchanged. Normal156second Bane expiry remains. Bind final-four assertion is compiled in Integration2 but not exercised by this focused family;full Warlock qualification remains separate.')
    e.write('echoing-input-retention-native-evidence.json',result,files,check);print('Echoing input inspection:12results/two genders/two reloads;Hex,resources and foreign states retain.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
