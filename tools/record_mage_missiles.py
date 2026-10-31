#!/usr/bin/env python3
"""Bind Skeletal Mage traveling projectiles and retained Necromancer checks."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/mage-missiles';sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    prev_path=ROOT/'tools/reference/weapon-start-native-evidence.json';prev=json.loads(prev_path.read_text());ref=prev['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/NecromancerMageMissiles.cs','tests/Sanctuary.Integration/MageMissileChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['MissileValues','NecromancerSummons']}
    assert inputs['birth']['copiedData']==old['copiedData']
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/weapon-start-effects/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'mage-missile-check':1,'mage-missile-reload-check':2,'mage-missile-live-result':3})
    assert sum(expected.values())==136 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Weapon start:751current monster PMH flags')==1 and log.count('Poison Dagger contact:real pre-HP notification')==3
    from probe_mage_missiles import build as probe
    probe(True)
    saved=json.loads((STAGE/'mage-missile-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['State']['Frame']==6 and saved['State']['Fault'] is None
    assert saved['State']['Packet']['Missile']=='necromage4' and saved['Seed']['Actor']==saved['Mage']
    assert saved['Seed']['Seed']!=((666<<32)|1) and saved['Life']>0
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','cb40d58:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        audits.append(a)
    assert [len(a['InputHashes']) for a in audits]==[823,21] and len(audits[0]['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),prev_path,baseline,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/probe_mage_missiles.py','tools/reference/mage-missile-source-evidence.json',
        'tests/check_mage_missiles.py','tests/check_mage_missiles_audits.py','docs/mage-missiles-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log',
        'birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','mage-missile-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Skeletal Mage traveling elemental projectiles',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=136,boots=3,actualReloads=2,saves=3,sourcePacketCasesPerBoot=1524,elementalTravelCases=4,intrinsicCriticalCases=2,controlCases=6,naturalConnectedMageCasts=3,
            finalSavedFrame=6,finalSavedGeneration=2,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[823,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['The existing mage attack windup now emits a real straight projectile instead of immediate damage plus a moving visual. Four element packets use current rows and the summon missile-level formula.',
            'The shared velocity/Q12/cell/do1 services drive delayed collision,nonhoming motion,interception,wall collision and expiry. Shared mitigation/cold/poison and intrinsic critical services deliver captured damage.',
            'Complete older getter/length/tier/impact-roll bodies match all1524element/derived-rank contexts. Poison has HitShift0 and a disabled physical min0/max1280 lane;the other elements have HitShift8.',
            'Actual critical/noncritical hits exclude physical shields and advance a separate saved actor stream. Six controls cover immunity,offline,facet change,source deletion,terrain and slow capture.',
            'A connected native session exercises natural windup and the automatic40ms clock once per boot. Exact packet/vector/cells/age/deadline and an already-advanced critical stream survive two real reloads. All130predecessor markers remain retained.'],
        remaining=['Native1000ms/half-cycle actor timing,full source AI/animation/component behavior,initial-step/global phase chronology and native geometry/room/candidate order remain unverified.',
            'Offline/facet/removed-source flights age without fighting. Original-engine retired-source lifetime/ownership/credit remains open;an optional user observation is pending. NPC/world seed chronology is not certified.',
            'Shared incoming/death/reaction ordering,modern initial poison-rate applicability,PvP/global difficulty projections and other recorded class gaps remain. Birth/life/passive/resistance/roster needs its own class audit.',
            'No new full-class run,complete skill/class,deployment or final assets are claimed. Continue the Necromancer summon/birth audit while nonblocking uncertainties remain documented.'])
    path=ROOT/'tools/reference/mage-missile-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Mage missiles:136markers/3boots/2reloads,1524source packet cases,3natural casts,4audits/71Python;installed artifacts unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
