#!/usr/bin/env python3
"""Bind Teeth's saved source-based flight and retained class regressions."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/teeth';sha=inventory.sha
def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/bone-spear-native-evidence.json';prev=json.loads(previous.read_text());ref=prev['inputInventories']['birth'];assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'tests/Sanctuary.Integration/TeethChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['NecromancerCombat','NecromancerSpellMissiles']}
    data='Data/Sanctuary/Balance/necromancer-summons.json';assert all(inputs['birth']['copiedData'][p]==h for p,h in old['copiedData'].items() if p!=data)
    current=json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text());prior=json.loads(subprocess.check_output(['git','show','44232da:data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))
    assert [r for r in current if r['key']!='teeth']==prior and sum(r['key']=='teeth' for r in current)==1
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/bone-spear/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'teeth-check':1,'teeth-reload-check':2,'teeth-right-check':3,'teeth-right-result':3,'teeth-quick-check':3,'teeth-quick-result':3})
    assert sum(expected.values())==202 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    from probe_teeth import build as probe
    probe(True)
    saved=json.loads((STAGE/'teeth-fixtures.json').read_text());flight,=saved['Flights'];lease,=saved['Leases']
    assert saved['Generation']==2 and flight['Frames']==50 and flight['Flags']==2080 and flight['Cast']==lease['Cast'] and flight['Fault'] is None
    assert saved['NearLife']<50000*256 and saved['FarLife']==50000*256 and lease['Target']==saved['Near']
    assert lease['UntilUtc']>flight['NextUtc']
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',826),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','44232da:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text()) and all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==size and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert len(json.loads((STAGE/'named-item-runtime-audit.json').read_text())['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present'];assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous,baseline,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/probe_teeth.py','tools/build_class_skills.py','tools/reference/teeth-source-evidence.json','tests/check_teeth.py','tests/check_teeth_audits.py',
        'data/Sanctuary/Balance/necromancer-summons.json','docs/teeth-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','teeth-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Teeth source-based saved flight',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=202,boots=3,actualReloads=2,saves=3,retainedPredecessorMarkers=187,sourceDamageCasesPerBoot=4080,sourceRouterCases=192,actualRightQuickCasts=6,finalSavedGeneration=2,finalSavedFrame=flight['Frame'],savedActiveDelay=True,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[826,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Learned Teeth now shares the saved player spell-flight controller with Bone Spear. The existing integer fan feeds captured Q12 vectors at3072FP8 velocity,50frames,Size1,activation0 and source magic endpoints. Accepted contact removes a tooth;delayed candidates are filtered before selection.',
            'Complete older creator/getters/writer/roll execute4080current Teeth contexts,excluding weapon stats and critical. Complete finder/router execute192contexts with historical JUSTHIT as a supplied service,four-frame allocation and accepted-impact removal.',
            'Actual paid learned windup/release,18tile target,coincident-candidate delay filtering,exact fourth-frame expiry,separate cast identities,eight geometry/lifecycle controls,Slow Missiles,insufficient mana and atomic invalid-lease restore pass. Old Spear JSON loads omitted additive fields.',
            'Six connected right/quick casts exercise natural flight and once-only payment. Two actual reloads preserve an injured target,remaining tooth,cast identity and active hit-delay lease without replay. All187predecessor markers remain.'],
        remaining=['The retained per-cast160ms delay adapter is explicitly unverified against current exact caster/cast/skill boundaries. Official2.7 establishes same-skill isolation only;older global JUSTHIT must not be claimed as current behavior.',
            'Native room/terrain/candidate order,provisional NPC footprints,first/global frame phase,full modern dispatcher/FCR,event/PvP/special-recipient and retired-owner/client equivalence remain open. Same-build reload and predecessor JSON compatibility do not certify an old-content binary upgrade.',
            'Full class completion,new full-class run,deployment and final effects/audio/talent/hotkey icons are not claimed. Next:Bone Spirit steering,then poison field/nova mechanics.'])
    path=ROOT/'tools/reference/teeth-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Teeth:202markers/3boots/2reloads,4080source damage/192router cases,6real inputs,4audits/71Python;installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
