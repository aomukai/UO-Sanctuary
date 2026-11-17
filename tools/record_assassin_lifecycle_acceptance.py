#!/usr/bin/env python3
"""Record current Assassin composition without recertifying historical artifacts."""
import argparse,json,re,sys,tarfile,hashlib
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/assassin-lifecycle-acceptance'
sys.path.insert(0,str(ROOT/'tests'))
from check_assassin_lifecycle_acceptance import configuration
from check_assassin_planned_acceptance import LEGACY

def expected():
    c=configuration();r=Counter()
    def add(n,count):
        r[n]+=count;results=c.get('extra_wait_results',{}).get(n,())
        for result in((results,)if isinstance(results,str)else results):r[result]+=count
    add('iron-golem-birth-check',3)
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
    for n in c['extra_checks']:add(n,3)
    for n in c.get('extra_pre_save_commands',()):add(n,3)
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    for n in LEGACY:r[n+'-input-check']+=3;r[n+'-input-result']+=3
    for n in (0,1,9):r[f'fury-held-wire-{n}-result']+=3
    r['fury-held-input-save-stage-check']+=3
    for n in ('onehand','twohand','onehand-immune','twohand-immune'):
        for suffix in ('-check','-assassin-check','-result'):r['sentinel-stealskull-'+n+suffix]+=3
    assert sum(r.values())==909
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/sentinel-stealskull-retained/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    assert born['artifacts']==parent['artifacts']and born['copiedData']==parent['copiedData']
    for p,d in born['sourceInputs'].items():
        assert e.sha(ROOT/p)==d,p
        if p.endswith('.cs'):assert parent['sourceInputs'][p]==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):
            assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    log=e.cohort(STAGE,expected(),909);assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
    previous=Counter(e.load(ROOT/'tools/reference/assassin-planned-acceptance-native-evidence.json')['markers']);assert not(previous-expected())and sum(previous.values())==655
    blades=Counter(e.load(ROOT/'tools/reference/sentinel-stealskull-link-native-evidence.json')['markers']);assert not(blades-expected())
    for boot in range(3):
        rows=e.load(STAGE/f'sentinel-stealskull-boot-{boot}.json');assert len(rows)==4
        for r in rows:
            assert r['Fault']is None and r['Typed']and r['Direct']==r['PrimaryLoss']and r['Shared']==r['SecondaryLoss']
            assert r['LifeDrain']==r['ManaDrain']==(1 if r['TwoHanded']else 3)
            if r['Immune']:assert r['Shared']==0 and r['Direct']==r['LifeDrain']
    audit=e.load(ROOT/'tools/reference/assassin-current-input-coverage.json');assert(audit['activeTalents'],audit['passiveTalents'],audit['verifiedActiveInputRoutes'],audit['heldInputSequences'],audit['legacyInputSequences'])==(28,2,28,9,21)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(named,assembly=STAGE/'content/Sanctuary.Content.dll')
    connections=log.count('Client: 127.0.0.1: Connected.');assert connections==299
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l]
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()]
    assert len(warnings)==1 and all('"WardrobeSet"'in l for l in warnings)
    assert len(unsupported)==9 and all('Defiler link sharing refused unsupported context'in l for l in unsupported)
    c=configuration();assert tuple(len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files'))==(80,30,58,100)
    ledger=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(ledger['skills'])==240 and not ledger['completedClasses']
    paths=['tools/record_assassin_lifecycle_acceptance.py','tools/review_assassin_input_coverage.py','tools/class_input_coverage.py','tools/reference/assassin-current-input-coverage.json','docs/assassin-lifecycle-acceptance-checkpoint.md','docs/assassin-coverage-matrix.md','docs/class-skill-roadmap.md','docs/class-skill-status-overview.md','docs/deferred-item-skill-effects.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[*(ROOT/p for p in paths),*c['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-native-1.tar.gz']
    result=dict(component='Current Assassin class/input/lifecycle and shared combat integration',referenceVersion='3.3.93847',baselineCommit='ef1561948589b8fed9d80a9c0ab4d7f26ce93443',nativeMarkers=909,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=655,fixtureFamilies=80,sharedChecks=30,ordinaryInputCommands=58,explicitAssassinInputCases=audit['ordinaryInputCases'],negativeInputCases=audit['negativeInputCasesSeparatelyExcluded'],foreignInputCases=audit['foreignInputCasesSeparatelyExcluded'],legacyScenarios=42,creditedLegacySequences=21,heldFurySequences=9,newCoopCasts=12,activeTalents=28,passiveTalents=2,connections=connections,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],productionCodeChanged=False,runtimeDataChanged=False,contentRebuilds=0,integrationRebuilds=0,nativeRebuilds=0,nativeRun=2,frozenDriverFailures=1,saveDeletionWarnings=len(warnings),intentionalScalarRefusalWarnings=len(unsupported),classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Retains655Assassin markers plus current martial/weapon/item-proc/spell/Blade typed Health Link and approved cold-corpse contracts. Explicit-class inputs,legacy Cast scenarios,held Fury and foreign controls remain separate. Seven legacy spell/buff routes bind class through compiled fixture ownership/default and normal availability guards,not an invented packet class byte. Reuses exact accepted production/integration/native/data without rebuild. Original actor/flight/charge/buff/roster/Fury save scopes retain;prepared component tests do not certify every intrinsic AI or modern D2R/PvP/geometry/item callback. Final effects/audio/icons and graphical-client acceptance remain separate.')
    e.write('assassin-lifecycle-acceptance-native-evidence.json',result,files,check)
    print(f'Assassin:909exact results/28active+2passive scopes/{connections}connections/two reloads;accepted build reused.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
