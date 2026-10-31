#!/usr/bin/env python3
"""Bind Mage birth/resources to exact build inputs and native reload evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/mage-birth';sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    prev_path=ROOT/'tools/reference/mage-missile-native-evidence.json';prev=json.loads(prev_path.read_text());ref=prev['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/NecromancerMageBirth.cs','tests/Sanctuary.Integration/MageBirthChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['NecromancerCombat','NecromancerSummons','SkeletonPets','SkillHealth']}
    assert inputs['birth']['copiedData']==old['copiedData']
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/mage-missiles/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'mage-birth-check':1,'mage-birth-reload-check':2,'summon-difficulty-check':1,'summon-difficulty-reload-check':2})
    assert sum(expected.values())==142 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    from probe_mage_birth import build as probe
    probe(True)
    saved=json.loads((STAGE/'mage-birth-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Life256']==9045 and saved['Maximum256']==17177 and saved['Rate']==16
    assert saved['Rank']==4 and saved['Mastery']==0 and saved['MissileRank']==1 and saved['Element'] in range(4)
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','e16177f:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        audits.append(a)
    assert [len(a['InputHashes']) for a in audits]==[824,21] and len(audits[0]['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),prev_path,baseline,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/probe_mage_birth.py','tools/reference/mage-birth-source-evidence.json',
        'tests/check_mage_birth.py','tests/check_mage_birth_audits.py','docs/mage-birth-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log',
        'birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','mage-birth-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Skeletal Mage birth and exact resources',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=142,boots=3,actualReloads=2,saves=3,sourceBirthCasesPerBoot=11475,retainedPredecessorMarkers=136,
            finalSavedGeneration=2,finalSavedLife256=9045,exactMageMaximum256=17177,regeneration256=16,auditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[824,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Complete older base/passive/resistance bodies and MAXHP callback branch with explicit current data/formula/stat services match11475owner/rank/difficulty/Mastery contexts. This is not a full modern initializer probe.',
            'Current fixed noRatio bases61/88/123,flat Mastery then rank percentage,level/armor/attack additions and derived missile rank retain exact FP256 values. Changed maxHP callbacks derive source regeneration;unchanged pools retain the spawn rate.',
            'A real learned rank4/mastery0 corpse cast pays once and consumes the corpse once. Exact life17177FP displays68HP;recovery16FP/40ms caps at that fractional maximum and respects PMH/parking.',
            'Actual facet notifications preserve exact injured ratios without recasting. Battle Orders changes maximum/current life and regeneration through shared callbacks. Owner gear/talent changes leave the existing captured ranks and resistance unchanged.',
            'Two real reloads preserve injured fractional life,maximum,rate,element,owner and captured ranks;recovery resumes without offline replay. Five-family difficulty checks and all136predecessor markers are retained.'],
        remaining=['Modern immunity-pierce consumers are unsupported;nonzero live Sunder inputs refuse before casting/payment. This increment tests the zero-input route,not an actual Sunder-item rejection.',
            'The source probe supplies zero absorption pools and resistance bonuses0/20/52/65/75. Live rank7 supplies52;other Summon Resist display rounding conflicts remain explicitly unresolved.',
            'Native1000ms/half-cycle mage timing,full source AI/animation/component/initializer,equipment generation,roster history,current-executable callbacks,poison-rate aggregation and retained-source missile lifetime/credit remain separate.',
            'Native HP ceiling65000 and facet ratio changes are Sanctuary adapters. Same-build reloads are verified;old-content binary migration,new full-class acceptance,deployment and final assets are not claimed. Revive retains living abilities;global monster spell conversion remains separate.'])
    path=ROOT/'tools/reference/mage-birth-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Mage birth:142markers/3boots/2reloads,11475source cases per boot,4audits/71Python;installed artifacts unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
