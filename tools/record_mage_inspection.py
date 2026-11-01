#!/usr/bin/env python3
"""Bind the compiled Mage inspector to the frozen retained regression."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/mage-inspection'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/f'{n}-inputs.json') for n in ('birth','audit')}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/f'{n}-inputs.json')
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/mage-basics-native-evidence.json'
    prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/SkeletonPets.cs','tests/Sanctuary.Integration/ClassNameChecks.cs','tests/Sanctuary.Integration/MageBasicChecks.cs','tests/Sanctuary.Integration/MageMissileChecks.cs'}
    assert before.keys()==after.keys() and {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/mage-basics/birth-native-checks.log'
    assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(markers(baseline).values())==388 and markers(STAGE/'birth-native-checks.log')==markers(baseline)
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('compiled per-bolt/volley inspector pass')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    targeted=ROOT/'.local/class-skills/mage-inspection-targeted/birth-native-checks.log'
    counts=markers(targeted);assert sum(counts.values())==15 and counts['mage-missile-live-result']==3 and counts['mage-missile-reload-check']==2
    assert ': failed' not in targeted.read_text() and ' ERR]' not in targeted.read_text()
    assert 'pass across3boots/2reloads' in (STAGE/f'targeted-run-{build}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in (('named-item-runtime-audit.json',886),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert len(audit['InputHashes'])==count and audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest
        previous=json.loads(subprocess.check_output(['git','show','62cf243:tools/reference/'+n],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    auditlog=(STAGE/'audit-native-checks.log').read_text()
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1}) and ': failed' not in auditlog and ' ERR]' not in auditlog
    assert 'exported from the selected build' in (STAGE/f'audit-run-{audit_run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==186 and sum(s['characterClass']=='Necromancer' for s in handlers)==30 and not status['completedClasses']
    docs=('mage-inspection-checkpoint','mage-basics-checkpoint','safe-area-minion-preflight','class-skill-reuse-notes','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit')
    files=[parent,baseline,targeted,ROOT/'.local/class-skills/mage-inspection-targeted/birth-inputs.json',Path(__file__).resolve(),ROOT/'tests/check_mage_inspection.py',*[ROOT/p for p in changed],*[ROOT/f'docs/{n}.md' for n in docs]]
    files += [p for p in STAGE.iterdir() if p.is_file()]
    result=dict(referenceVersion='3.3.93847',component='Compiled Mage base spell damage per bolt and live volley count',installed=False,goalComplete=False,completedClasses=[],
        build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=388,retainedMarkers=388,boots=3,actualReloads=2,saves=3,targetedNativeMarkers=15,targetedLiveSessions=3,compiledRankMasteryElementContexts=48,connectedElementInspections=9,pythonTests=71,namedDefinitions=573,auditMarkers=4,auditFingerprints=[886,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/f'{n}-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'The actual PetStatsGump compiles captured-rank Sorceress basic damage including the approved flat Mastery addition,with fractional endpoints,element,per-bolt units and separate live volley size. Other pet damage text is retained. Production combat/profile/life/save/runtime-data behavior is unchanged.',
            'The retained natural Mage missile fixture now observes pending source action/end frames and verifies the reconstructed emission deadline exactly,instead of comparing two host timer callbacks with a400..800mswall window. It retains pre-action HP/no-flight,release-before-cycle-end and actual collision/removal checks.15focused native markers/three live sessions/two reloads pass with no production timing change.',
            'The existing compressed-gump decoder is shared with class-name tests.48independent old-source-delta/Sorceress contexts,real soft4+1/hard5changes and nine connected element inspections pass inside the retained388-marker mechanical/save regression.Three boots/two reloads/three saves,clean builds,four fresh audits,71Python/573named/eight-class240pass.886/21fingerprints preserve decisions;kernel/data/installed106unchanged.'],
        remaining=[
            'Final client rendering,broader summon roster headings,other typed pet damage,secondary spell/buff descriptions and live Slow Missiles display are outside this increment.',
            'Earlier mechanical/reference/event/PvP/Sunder/source-removal and old-DLL/concurrent/unheld-save limits remain. Final effects/audio and icons remain separately tracked. Safe-area parking,barriers and remaining Necromancer/class roadmap work continue;no class is complete.'])
    output=ROOT/'tools/reference/mage-inspection-native-evidence.json'
    rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Mage inspection:compiled per-bolt/volley display,388retained markers/2reloads;installed unchanged. No class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('build','run','audit-run'):p.add_argument('--'+name,type=int,required=True)
    p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
