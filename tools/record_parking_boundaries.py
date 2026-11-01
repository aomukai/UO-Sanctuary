#!/usr/bin/env python3
"""Bind legacy/safe-facet/wardrobe parking acceptance to frozen evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/parking-boundaries'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/f'{n}-inputs.json') for n in ('birth','audit')}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/f'{n}-inputs.json')
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/safe-area-minions-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'tests/Sanctuary.Integration/FullWardrobeChecks.cs','tests/Sanctuary.Integration/ReviveFacetChecks.cs'}
    added={'tests/Sanctuary.Integration/ParkingBoundaryChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added and {p for p in before if before[p]!=after[p]}==changed
    assert {p:h for p,h in before.items() if p.startswith('src/')}=={p:h for p,h in after.items() if p.startswith('src/')}
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/safe-area-minions/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==394
    for family in ('parking-boundary','full-wardrobe','revive-facet'):
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    expected['parking-boundary-live-result']+=3
    assert sum(expected.values())==406 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Parking boundaries:five original legacy kinds')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    targeted=ROOT/'.local/class-skills/parking-boundaries-targeted/birth-native-checks.log';counts=markers(targeted)
    assert sum(counts.values())==21 and counts['parking-boundary-live-result']==3
    for family in ('parking-boundary','full-wardrobe','revive-facet'):assert counts[family+'-reload-check']==2
    assert ': failed' not in targeted.read_text() and ' ERR]' not in targeted.read_text()
    assert 'pass across3boots/2reloads' in (STAGE/f'targeted-run-{build}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in (('named-item-runtime-audit.json',887),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert len(audit['InputHashes'])==count and audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest
        previous=json.loads(subprocess.check_output(['git','show','d51dca2:tools/reference/'+n],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ': failed' not in (STAGE/'audit-native-checks.log').read_text() and 'exported from the selected build' in (STAGE/f'audit-run-{audit_run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest
    saved=json.loads((STAGE/'parking-boundary-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Pets'])==5 and len(saved['Revives'])==2
    assert all(0<p['Life']<p['Maximum'] for p in saved['Pets']) and len({p['Serial'] for p in saved['Pets']}|{p['Actor'] for p in saved['Revives']})==7
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==186 and sum(s['characterClass']=='Necromancer' for s in handlers)==30 and not status['completedClasses']
    docs=('parking-boundaries-checkpoint','safe-area-minions-checkpoint','pet-adjustment-review','class-skill-reuse-notes','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons')
    files=[parent,baseline,targeted,ROOT/'.local/class-skills/parking-boundaries-targeted/birth-inputs.json',Path(__file__).resolve(),ROOT/'tests/check_parking_boundaries.py',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    files += [p for p in STAGE.iterdir() if p.is_file()]
    result=dict(referenceVersion='3.3.93847',component='Legacy Necromancer/safe-facet parking,real wardrobe compatibility and original Revive facet acceptance',installed=False,goalComplete=False,completedClasses=[],productionChanged=False,build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=406,retainedMarkers=394,boots=3,actualReloads=2,saves=3,targetedNativeMarkers=21,connectedSafeFacetSessions=3,savedLegacyActors=5,savedOriginalRevives=2,pythonTests=71,namedDefinitions=573,auditMarkers=4,auditFingerprints=[887,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/f'{n}-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/f'{n}-inputs.json')) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Five original legacy summon kinds and two actual learned original Revives remain internal on real guarded Normal/Nightmare/Hell facets. Native location/map callbacks preserve identity,ranks/mastery/order/original slot costs and independently checked injured FP256 percentage. Same-difficulty entry retains original birth state;actual changes reuse existing Revive life/stat assertions. Repeated sync leaves exact resources unchanged and owner mana/action deadlines are retained.',
            'Three connected cohorts cross from safe Normal to safe Hell,remain idle without recovery,and return through the actual cross-facet exit hook. Seven retained actors survive two real reloads and safe-area login/resume without cloning,healing or loss of captured state.',
            'The complete existing wardrobe suite retains actual public transactions,gear storage/rollback/all histories/resource-deadline carry/follower-cap refusal/ordinary pets/owner death/inactive respec/absolute expiry. Restored classes obey current safety admission;explicit field returns prove wounds/slot restoration. Costs are assigned while active rather than changing an already parked zero-slot actor. Original Revive facet coverage is retained separately. Production admission/capacity rules are unchanged.',
            'Only two existing integration fixtures and one new integration file change. Production C#,runtime data,native kernel and installed106are unchanged. Clean builds,21focused markers/3boots/2reloads,406full markers retaining394/3saves,four fresh audits,71Python/573named/eight-class240pass.887/21fingerprints preserve decisions. No deployment/class completion.'],
        remaining=[
            'All Druid species,custom safe-house/region authoring,additional legacy/current controller combinations,every respec/expiry edge,final client and installed old-DLL/concurrent/unheld-save scopes remain. Existing summon/source/event/PvP/Sunder/barrier limits remain explicit.',
            'The manually constructed simultaneous legacy golems are independent component controls,not permission to bypass the shared gameplay cap. No new art or binding is approved;effects/audio and talent/hotkey icons remain separate. Continue remaining Necromancer mechanics and class roadmap;no class is complete.'])
    output=ROOT/'tools/reference/parking-boundaries-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Parking boundaries:406markers/2reloads,legacy safe facets/wardrobe/original Revives;production and installed unchanged. No class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('build','run','audit-run'):p.add_argument('--'+name,type=int,required=True)
    p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
