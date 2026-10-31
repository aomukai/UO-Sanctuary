#!/usr/bin/env python3
"""Bind Bone Spear's saved source-based flight and retained class regressions."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bone-spear';sha=inventory.sha
def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/bone-armor-native-evidence.json';prev=json.loads(previous.read_text());ref=prev['inputInventories']['birth'];assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/NecromancerSpellMissiles.cs','tests/Sanctuary.Integration/BoneSpearChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['NecromancerCombat','ClassDeath','CharacterProfiles','WardrobeTransaction']}
    data='Data/Sanctuary/Balance/necromancer-summons.json';assert all(inputs['birth']['copiedData'][p]==h for p,h in old['copiedData'].items() if p!=data)
    current=json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text());prior=json.loads(subprocess.check_output(['git','show','a17333c:data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))
    assert [r for r in current if r['key']!='bonespear']==prior and sum(r['key']=='bonespear' for r in current)==1
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/bone-armor/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'bone-spear-check':1,'bone-spear-reload-check':2,'bone-spear-right-check':3,'bone-spear-right-result':3,'bone-spear-quick-check':3,'bone-spear-quick-result':3})
    assert sum(expected.values())==187 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    from probe_bone_spear import build as probe
    probe(True)
    saved=json.loads((STAGE/'bone-spear-fixtures.json').read_text());flight=saved['Flight']
    assert saved['Generation']==2 and flight['Frame']==9 and flight['LastCollider']==saved['First'] and flight['Seed']!=((666<<32)|1) and flight['Fault'] is None
    assert saved['FirstLife256']<50000*256 and saved['Life256']==50000*256
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',826),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','a17333c:tools/reference/'+name],cwd=ROOT))
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
    paths += [ROOT/p for p in ['tools/probe_bone_spear.py','tools/build_class_skills.py','tools/reference/bone-spear-source-evidence.json','tests/check_bone_spear.py','tests/check_bone_spear_audits.py',
        'data/Sanctuary/Balance/necromancer-summons.json','docs/bone-spear-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','bone-spear-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Bone Spear source-based saved flight',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=187,boots=3,actualReloads=2,saves=3,retainedPredecessorMarkers=172,sourceDamageCasesPerBoot=4080,sourceRouterCases=96,actualRightQuickCasts=6,finalSavedGeneration=2,finalSavedFrame=9,advancedSeedAndCollider=True,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[826,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Bone Spear uses the current192row through shared factory velocity,Q12 vectors,retained source cells,do1 updates,FP magic rolls,spell defenses and health. It has40frames,unlimited ordinary piercing and only the latest collider identity;the provisional world-distance cap and permanent hit-set are removed.',
            'Complete older damage creator/getters/stat writer/roll execute4080current contexts without collecting supplied weapon/item/origin stats or critical. Complete generic hit routing executes96contexts with explicit event/packet/defense services.',
            'Actual learned casting pays once at release. Tests cover travel beyond16tiles,three intervening targets,rehitting a moved prior collider,wall/moving-target misses,offline/facet/death/respec/wardrobe behavior,captured Slow Missiles and actual Hell Lich Lord magic immunity without stopping penetration.',
            'Real dagger/Raven Frost weapon-element exclusions,Wormskull/hard-synergy recapture and atomic malformed restore pass. The actual tier3spell potion adds four captured10percent channels exactly once,even after its live buff is cleared.',
            'Six connected right/quick casts exercise automatic travel/collision and payment. Two actual reloads retain midflight pose,packet,source steps,last collider and an advanced RNG stream;the save serializer snapshots on the main thread before the writer runs. All172predecessor markers remain.'],
        remaining=['Native room/terrain/candidate ordering,provisional NPC footprints,exact first/global frame phase,full modern factory/dispatcher/FCR and graphical-client equivalence remain unverified. Existing live-target/release-range rules and default self-aim fallback are retained adapters.',
            'Generic source hit events,full incoming/death/reaction ordering and PvP/special recipient coverage remain scoped shared adapters. Current custom magic mastery/pierce and approved potion rules are preserved separately from the SrcDam0 source proof;no weapon on-strike or drain behavior is invented.',
            'Offline flights age without fighting;death/respec/wardrobe cancel and facet change removes. Complete current retired-owner lifetime/credit equivalence remains open. Same-build reloads are verified;old-content binary migration,new full-class run,deployment,complete skill/class and final assets are not claimed.',
            'Next:reuse the player spell-flight foundation for Teeth while resolving its distinct next-hit window,then Bone Spirit steering and poison field/nova allocation.'])
    path=ROOT/'tools/reference/bone-spear-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Bone Spear:187markers/3boots/2reloads,4080source damage cases,6real inputs,4audits/71Python;installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
