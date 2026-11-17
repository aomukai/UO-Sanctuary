#!/usr/bin/env python3
"""Record supplied-index Pierce creation and retained learned passive lifecycle."""
import argparse,hashlib,json,subprocess,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pierce-source-seed';BASE='c79acd8cf0e8bcd064940a0b3edc6b8d2404ba0d'
def expected_markers():
    r=Counter({'iron-golem-birth-check':3,'pierce-seed-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks','hellslayer-readiness','amazon-pierce'):r[f+'-check']=1;r[f+'-reload-check']=2
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/impale-defense-policy/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/NamedProjectiles.cs','tests/Sanctuary.Integration/PierceSeedChecks.cs'],changed
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/name)==born['artifacts'][name]==parent['artifacts'][name]
    for kind in ('Content','Integration'):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];text=(STAGE/(kind.lower()+'-build-1.log')).read_text();assert all(t in text for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),18);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert 'Client: 127.0.0.1: Connected.' not in log
    assert not list(STAGE.glob('failed*.tar.gz'))
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert not warnings,warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    from probe_pierce_source_seed import record as review
    review(True);cases=e.load(STAGE/'source-cases.json');assert len(cases)==936 and {r[3]for r in cases}==set(range(5))
    # The supplied-index primitive has no live launch caller yet.
    uses=[str(p.relative_to(ROOT))for p in (ROOT/'src/Sanctuary.Content').glob('*.cs')if 'SeededPierceBudget(' in p.read_text()];assert uses==['src/Sanctuary.Content/NamedProjectiles.cs']
    saved=e.load(STAGE/'amazon-pierce-fixtures.json');assert saved['Flight']['Pierces']==3 and saved['FirstHealth']>0 and saved['SecondHealth']>0
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert status==json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    before=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==before['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    files=[ROOT/'tools/record_pierce_source_seed.py',ROOT/'tools/probe_pierce_source_seed.py',ROOT/'tools/reference/pierce-source-seed-review.json',ROOT/'tests/check_pierce_source_seed.py',ROOT/'docs/pierce-source-seed-checkpoint.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*e.stage_files(STAGE),STAGE/'source-probe.cpp',STAGE/'accepted-assemblies-1.tar.gz']
    files.extend(ROOT/p for p in born['sourceInputs']if not p.endswith('.cs'));files.extend(ROOT/p for p in changed)
    result=dict(component='Supplied owner-index Pierce seed calculation and learned passive regression',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=18,markers=dict(sorted(expected_markers().items())),sourceVectors=936,fanRepeatSize=24,legacyInputWriters=12,connections=0,newWireInputs=0,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,failedAttempts=0,saveDeletionWarnings=warnings,nativeRebuilt=False,persistenceVersionChanged=False,onlineSeedSupplierChanged=False,installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='The unchanged older factory creation block,actual seed initializer,item RNG wrapper and seed-roll body compile with transparent observation and fixed-width seed declaration. Native helper reuses existing budget loop and SourceRandom,matching936budget/draw/final-seed rows and same-index24missile repetitions without touching live world/player/NPC streams. Zero chance has no seed/read/draw;nonnegative100+ still consumes4draws;negative total is explicitly refused by the new helper. Existing learned Pierce rank/real item/additive chance/binding/history/live eligibility/actual original flight/gear/HP saves retain through2reloads. Twelve older player callbacks increment the base index;coordinate initialization in FirstFn is monster-only. The new-protocol input/hold/release mapping,initial player index and saved owner-index policy remain unresolved;no online caller uses the supplied-index helper yet. Existing launch RNG adapter remains. Modern3.3executable seed/fan/global equivalence and all other recorded class/item/client/asset limits remain open.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/pierce-source-seed')
    e.write('pierce-source-seed-native-evidence.json',result,files,check);print('Pierce seed:936real RNG vectors/18native results/24same-index repeats/2reloads;online input-index mapping remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
