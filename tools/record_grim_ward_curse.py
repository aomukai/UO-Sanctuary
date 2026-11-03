#!/usr/bin/env python3
"""Bind the Ward curse operation while preserving its unresolved live context."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/grim-ward-curse'
SOURCE=ROOT/'.local/class-skills/grim-ward-curse-source';PARENT='8e7898a6f1b8059cba8baf3132c40ac83b2f69b4';sha=inventory.sha
def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-2.json',STAGE/(name+'-inputs.json'))
    before=json.loads((STAGE/'pre-build-1.json').read_text())['inputs'];after=json.loads((STAGE/'pre-build-2.json').read_text())['inputs']
    assert before['sourceInputs']==after['sourceInputs'] and before['projectInputs']==after['projectInputs']
    assert {n for n in before['copiedData'] if before['copiedData'][n]!=after['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/pet-placement-world/birth-inputs.json').read_text())
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/GrimWardCurseOperation.cs','tests/Sanctuary.Integration/GrimWardCurseChecks.cs'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    sourcepath=ROOT/'tools/reference/grim-ward-curse-source-evidence.json';source=json.loads(sourcepath.read_text())
    assert source['referenceVersion']=='3.3.93847' and source['cases']==31134 and source['sanitizers']=='ASan/UBSan'
    assert not any(source[k] for k in ('handlerEnabled','currentExecutableParity','processMemoryRead'))
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
    fixture=source['fixture'];assert native['sourceInputs'][fixture['path']]==fixture['sha256']==sha(ROOT/fixture['path'])
    rows=json.loads((ROOT/fixture['path']).read_text());assert len(rows)==31134 and sum(r[1][0] for r in rows)==5184
    assert sum(1 for p,r in rows if r[0] and r[1][3]==999)==1296
    assert sum(1 for p,r in rows if r[0] and p[6]>=100 and p[7]==0)==864
    assert (SOURCE/'sanitizer.log').read_text()=='' and '31134 complete legacy curse-path comparisons' in (SOURCE/'source-run-1.log').read_text()
    originalRecipe=SOURCE/'checker-original-1';oldProof=json.loads((originalRecipe/'source-evidence.json').read_text())
    assert sha(originalRecipe/'probe_grim_ward_curse.py')==oldProof['artifacts']['tools/probe_grim_ward_curse.py']
    assert oldProof['fixture']==source['fixture'] and {n for n in oldProof['artifacts'] if oldProof['artifacts'][n]!=source['artifacts'][n]}=={'tools/probe_grim_ward_curse.py'}
    assert 'verified;no files changed.' in (SOURCE/'source-check.log').read_text()
    baseline=ROOT/'.local/class-skills/pet-placement-world/birth-native-checks.log'
    previous=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-placement-world-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==previous['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['grim-ward-curse-check']+=3;expected['grim-ward-lifecycle-check']+=3
    assert sum(expected.values())==152 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Ward curse:31134complete legacy list/operation comparisons,5184accepted,1296equal-rank creator/primary retentions,864immune reductions;')==3
    assert log.count('Grim Ward lifecycle:3144complete legacy-source comparisons,167elapsed pulse values;')==3
    assert all(log.count(f'Pet placement world:generation{g},same saved registry/two original pets;')==1 for g in range(3))
    world=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text());assert world['Generation']==2 and json.loads(world['State'])['Revision']==11
    assert len(world['Bodies'])==2
    reconnect=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());assert reconnect['Generation']==2 and reconnect['TotalDraws']==10 and reconnect['Life']==69247 and reconnect['Mana']==54272
    for name,count in (('named-item-runtime-audit.json',949),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in current['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-1.log')).read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    ward=next(s for s in status['skills'] if s['talentId']=='barbarian.grimward');assert ward['implementation']=='blocked' and ward['verification']=='partial-native-coverage' and not ward['complete']
    intermediate=STAGE/'intermediate-context-2';oldPackage=json.loads((intermediate/'birth-inputs.json').read_text());assert oldPackage['artifacts']==native['artifacts']
    oldStatus=json.loads((intermediate/'class-skill-status.json').read_text());oldWard=next(s for s in oldStatus['skills'] if s['talentId']=='barbarian.grimward')
    assert oldWard['verification']=='not-started';oldWard['verification']='partial-native-coverage';assert oldStatus==status
    inspection=SOURCE/'pet-mode-availability.json';available=json.loads(inspection.read_text())
    assert available['directCalleeReferencesVerified'] and available['processMemoryBytesRead']==0 and sum(len(r['fragments']) for r in available['records'])==8
    assert [r['bytes'] for r in available['records']]==[346,1752] and all(not f['readableExecutable'] for r in available['records'] for f in r['fragments'])
    for name,h in available['artifacts'].items():assert sha(ROOT/name)==h,name
    diagnostic=STAGE/'diagnostic-manifest-1';assert 'grim-ward-lifecycle-fixtures.json' in (diagnostic/'native-run-1.log').read_text()
    assert 'FileNotFoundError' in (diagnostic/'native-run-1.log').read_text() and 'grim-ward-curse-check: passed' in (diagnostic/'birth-native-checks.log').read_text()
    for kind in ('content','integration'):assert sha(diagnostic/kind/('Sanctuary.'+kind.title()+'.dll'))==sha(STAGE/kind/('Sanctuary.'+kind.title()+'.dll'))
    fixed=(diagnostic/'check_grim_ward_curse.py').read_text().replace("'pet-placement-world','grim-ward-lifecycle')","'pet-placement-world')").replace("extra_checks=('grim-ward-curse-check'","extra_checks=('grim-ward-lifecycle-check','grim-ward-curse-check'")
    assert fixed==(ROOT/'tests/check_grim_ward_curse.py').read_text()
    files=[Path(__file__).resolve(),sourcepath,inspection,ROOT/'tools/reference/grim-ward-research-context.json',ROOT/'tests/check_grim_ward_curse.py',baseline,SOURCE/'source-run-1.log',SOURCE/'source-run-2.log',SOURCE/'source-check.log',
        *(ROOT/'docs'/n for n in ('grim-ward-curse-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-3.log','audit-native-checks.log','audit-run-2.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','pet-placement-world-fixtures.json','pet-native-placement-fixtures.json','goatman-body-fixtures.json','pet-reconnect-fixture.json','pet-obstacle-fixture.json')),
        *(p for p in diagnostic.rglob('*') if p.is_file()),*(p for p in (STAGE/'intermediate-context-2').iterdir() if p.is_file()),*(p for p in originalRecipe.iterdir() if p.is_file())]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Prepared Ward curse stat-list results and ordered operation plan with explicit formula context',
        verified=dict(legacyCurseCasesPerBoot=31134,acceptedPerBoot=5184,retainedPrimaryCreatorPerBoot=1296,immuneReductionsPerBoot=864,lifecycleCasesPerBoot=3144,pulses=167,
            nativeMarkers=152,retainedNativeMarkers=146,boots=3,reloads=2,saves=3,pythonTests=71,namedDecisions=573,auditFingerprints=[949,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Complete legacy curse bodies execute with supplied formula,AI permission,one radial visit and stat-list/event services. Native compares exposed result fields and ordered operation plans;this is not actual Ward AI/curse actor transport or current executable parity.',
            'New method takes explicit formula context and active prior lists. It does not choose current missile owner lookup,birth-versus-live hard points,creator/target AI,priority,terrain,child clock or learned casting. Mode initializer/dispatcher unavailable:8non-RX fragments,0process-memory bytes read. Public research corroborates older behavior/intended synergy without resolving current mechanics. No new handler,class completion,deployment or final-art claim.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    target=ROOT/'tools/reference/grim-ward-curse-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Ward curse:31134legacy comparisons per boot;152markers/3boots/2reloads;original actors/71Python/573named/106installed retain;live context/world/learned casting remain open.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
