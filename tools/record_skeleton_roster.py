#!/usr/bin/env python3
"""Bind learned skeleton/Mage FIFO replacement to native casts and reloads."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/skeleton-roster';sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/mage-birth-native-evidence.json';prev=json.loads(previous.read_text());ref=prev['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/NecromancerSkeletons.cs','tests/Sanctuary.Integration/SkeletonRosterChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['BattleCommand','NecromancerSummons','SkeletonPets']}|{'tests/Sanctuary.Integration/BattleCommandChecks.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/mage-birth/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'skeleton-roster-check':1,'skeleton-roster-reload-check':2,'battle-command-check':3,'battle-command-result':3})
    assert sum(expected.values())==151 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    from probe_skeleton_roster import build as probe
    probe(True)
    saved=json.loads((STAGE/'skeleton-roster-fixtures.json').read_text())
    assert saved['Generation']==2 and len(saved['Pets'])==4 and len({p['Serial'] for p in saved['Pets']})==4
    assert sorted(Counter(p['Kind'] for p in saved['Pets']).values())==[2,2] and all(p['Order']>0 and p['Rank']==2 and p['Life256']>0 for p in saved['Pets'])
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','2751970:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        audits.append(a)
    assert [len(a['InputHashes']) for a in audits]==[825,21] and len(audits[0]['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous,baseline,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/probe_skeleton_roster.py','tools/reference/skeleton-roster-source-evidence.json',
        'tests/check_skeleton_roster.py','tests/check_skeleton_roster_audits.py','docs/skeleton-roster-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log',
        'birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-roster-fixtures.json','failed-birth-run-1.log','failed-birth-inputs-1.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned skeleton and Mage FIFO rosters',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=151,boots=3,actualReloads=2,saves=3,sourceRosterCasesPerBoot=1530,retainedPredecessorMarkers=142,
            naturalBattleCommandRuns=3,finalSavedGeneration=2,savedFamilyMembers=4,auditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[825,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Complete unchanged older cap-setter/append bodies execute1530contexts spanning255current skill ranks and six counts per rank with reversed GUIDs. Current tables keep skeleton and skeletonmage separate,ungrouped pet types.',
            'Both learned creation paths and Battle Command refresh now use the existing saved birth-order ledger. Cap changes remove all excess oldest members;new candidates append after completed setup. Historical missing orders sort before recorded births with a disclosed serial tiebreak.',
            'Actual corpse casts distinguish serial from restored order,pay and consume once,preserve unrelated family/golem/owner members,and refuse insufficient mana without roster/corpse changes. Parked historical members are pruned without being promoted to newer orders.',
            'Two real reloads preserve both families,exact saved order,captured ranks and injury. Actual post-reload casts remove the persisted oldest entry. All142predecessor markers and three full Battle Command synchronous/natural-wave/expiry runs pass.',
            'The first added Battle Command natural-wave run failed;its synchronous setup could consume the timer slice. Starting that existing timed section on FixtureClock.NextLoop preserves its movement/range/expiry assertions. Failed-run logs and inputs remain recorded.'],
        remaining=['Original birth order of legacy untracked actors cannot be recovered;order0/serial is an explicit fallback. Historical-class activation limits,gear-only cap refresh triggers,full current initializer and source destruction/network event parity remain separate.',
            'This change preserves existing casting/payment/placement timing. Full transactional allocation/callback failure equivalence,source AI/animation clocks,current executable equivalence,shared incoming/death/reaction and earlier Sunder/poison gaps remain open.',
            'No deployment,new full-class run,old-content binary migration,complete skill/class or final assets are claimed. Revive inherits living abilities;global monster spell conversion remains separate.'])
    path=ROOT/'tools/reference/skeleton-roster-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Skeleton roster:151markers/3boots/2reloads,1530source cases per boot,3natural Command runs,4audits/71Python;installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
