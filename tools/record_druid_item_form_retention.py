#!/usr/bin/env python3
"""Record five saved-form inspection holds without claiming combined acceptance."""
import argparse,hashlib,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-item-form-retention'
FULL=ROOT/'.local/class-skills/druid-lifecycle-acceptance'
NAMES=('DruidBasicProc','DruidCapturedProc','DruidActionProc','DruidSetupItem','DruidWeaponPoison')
FAMILIES=('druid-basic-proc','druid-captured-proc','druid-action-proc','druid-setup-items','druid-weapon-poison')

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');old=e.load(ROOT/'.local/class-skills/assassin-lifecycle-acceptance/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');assert born['copiedData']==old['copiedData']
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in old['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    wanted=sorted('tests/Sanctuary.Integration/'+n+'Checks.cs'for n in NAMES)
    assert sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and old['sourceInputs'].get(p)!=d)==wanted
    with tarfile.open(FULL/'failed-native-1.tar.gz')as a:
        for p in wanted:
            original=a.extractfile(p).read().decode();assert hashlib.sha256(original.encode()).hexdigest()==old['sourceInputs'][p]
            needle='var point=p.Location;ClassCombat.Stop(p);p.Internalize();'if p.endswith('/DruidBasicProcChecks.cs')else'var point=p.Location;p.Internalize();'
            assert (ROOT/p).read_text()==original.replace(needle,'DruidFormWorldChecks.RetainForInspection(p);'+needle,1)
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for n in FAMILIES:expected[n+'-check']=1;expected[n+'-reload-check']=2
    log=e.cohort(STAGE,expected,24);assert 'pass across3boots/2reloads'in(STAGE/'native-run-1.log').read_text()
    assert ': failed'not in log and 'Warning: Attempted to delete'not in log
    assert all(t in(FULL/'integration-build-2.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    files=[ROOT/'tools/record_druid_item_form_retention.py',ROOT/'tests/check_druid_item_form_retention.py',ROOT/'docs/druid-lifecycle-acceptance-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',*(ROOT/p for p in wanted),*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',FULL/'failed-native-1.tar.gz',FULL/'integration-build-1.log',FULL/'integration-build-2.log',FULL/'source-snapshot-2.json',FULL/'source-snapshot-3.json']
    result=dict(component='Five Druid item/form inspection fixtures retain original captured state across saves',referenceVersion='3.3.93847',nativeMarkers=24,markers=dict(sorted(expected.items())),boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedTestSources=wanted,integrationBuild=2,productionChanged=False,runtimeDataChanged=False,nativeRebuilt=False,failedCombinedRunFrozen=True,classCertified=False,combinedAcceptancePending=True,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Existing RetainForInspection helper shifts both UTC endpoints only after actual contacts;asserts captured duration/payload/body/maxima/fractional resources. All original gear,proc-stream,wound,poison,setup and mana comparisons remain. Ordinary460second forms expired before the longer combined inspection;no gameplay duration/reset/recast change. This24result focus does not claim the pending1139result combined run passed.')
    e.write('druid-item-form-retention-native-evidence.json',result,files,check)
    print('Druid item/form retention:24exact results/two reloads;five test-only helper calls,combined acceptance separate.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
