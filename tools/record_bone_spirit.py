#!/usr/bin/env python3
"""Bind Bone Spirit's saved source-based flight and retained class regressions."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bone-spirit';sha=inventory.sha
def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/teeth-native-evidence.json';prev=json.loads(previous.read_text());ref=prev['inputInventories']['birth'];assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/BoneSpiritGuidance.cs','tests/Sanctuary.Integration/BoneSpiritChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['NecromancerCombat','NecromancerSpellMissiles','NecromancerEffects','CombatPotions']}|{'tests/Sanctuary.Integration/'+p+'.cs' for p in ['FanChecks','ClassSkillChecks','BoneSpearChecks']}
    data='Data/Sanctuary/Balance/necromancer-summons.json';assert all(inputs['birth']['copiedData'][p]==h for p,h in old['copiedData'].items() if p!=data)
    current=json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text());prior=json.loads(subprocess.check_output(['git','show','34d6d1c:data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))
    assert [r for r in current if r['key']!='bonespirit']==prior and sum(r['key']=='bonespirit' for r in current)==1
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/teeth/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'bone-spirit-check':1,'bone-spirit-reload-check':2,'bone-spirit-right-check':3,'bone-spirit-right-result':3,'bone-spirit-quick-check':3,'bone-spirit-quick-result':3,'fan-check':3,'fan-result':3,'class-skills-projectiles-check':3,'class-skills-projectiles-result':3})
    assert sum(expected.values())==229 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    from probe_bone_spirit import build as probe
    probe(True)
    saved=json.loads((STAGE/'bone-spirit-fixtures.json').read_text());flights=saved['Flights']
    assert saved['Generation']==2 and len(flights)==2 and {f['Guided']['Mode'] for f in flights}=={1,5} and saved['Life']==50000*256
    assert all(f['Fault'] is None and f['Guided']['Remaining']==f['Frames']-f['Frame'] and f['Guided']['Target']==saved['Target'] for f in flights)
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',827),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','34d6d1c:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text()) and all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==size and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert len(json.loads((STAGE/'named-item-runtime-audit.json').read_text())['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    full=inventory.validate_inventory(STAGE/'class-inputs.json');snapshot(STAGE/f'pre-build-{build}.json',STAGE/'class-inputs.json')
    assert full['artifacts']==inputs['birth']['artifacts'] and full['copiedData']==inputs['birth']['copiedData']
    baselineFull=ROOT/'.local/class-skills/npc-high-level-resources/class-native-checks.log'
    baselineRef=ROOT/'tools/reference/npc-high-level-resources-native-evidence.json';baselineEvidence=json.loads(baselineRef.read_text())
    assert sha(baselineFull)==baselineEvidence['artifacts'][str(baselineFull.relative_to(ROOT))]
    assert markers(STAGE/'class-native-checks.log')==markers(baselineFull) and sum(markers(baselineFull).values())==1180
    fullLog=(STAGE/'class-native-checks.log').read_text()
    assert ': failed' not in fullLog and ' ERR]' not in fullLog and fullLog.count('Shutting down')==3 and fullLog.count('Writing world save snapshot done')==35
    assert 'including two save/reload cycles.' in (STAGE/f'full-run-{run}.log').read_text()
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present'];assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous,baseline,baselineRef,baselineFull,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/probe_bone_spirit.py','tools/build_class_skills.py','tools/reference/bone-spirit-source-evidence.json','tests/check_bone_spirit.py','tests/check_bone_spirit_audits.py',
        'data/Sanctuary/Balance/necromancer-summons.json','docs/bone-spirit-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/t29-combat-potions.md','tools/reference/poison-spell-potion-deviation.json','docs/poison-nova-source-preparation.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','bone-spirit-fixtures.json','class-inputs.json','class-native-checks.log',f'full-run-{run}.log']]
    result=dict(referenceVersion='3.3.93847',component='Learned Bone Spirit source-based saved flight',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=229,boots=3,actualReloads=2,saves=3,fullRegressionMarkers=1180,fullRegressionSaves=35,fullRegressionBoots=3,fullRegressionReloads=2,retainedPredecessorMarkers=202,sourceDamageCasesPerBoot=4080,sourceLaunchCases=360,sourceSteerCases=2016,sourceHitCases=288,sourceSelectorCases=40,actualRightQuickCasts=6,finalSavedGeneration=2,savedModes=[1,5],savedGuidanceAndTargets=True,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[827,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Learned Bone Spirit shares saved spell packets,source-cell flight,FP health and lifecycle with Spear/Teeth. Guidance reuses GuidedArrows turn/hit/initial-frame primitives,with separate source-zero spell damage and no weapon AR/Pierce/proc payload.',
            'Complete older launch/factory/turn/hit/selector/distance bodies execute360launch,2016steer,288hit and40selection contexts using current Bone Spirit rows. Complete packet creator/getters/writer/roll execute4080contexts. Native launch capture compares144exact two-subtile projections;shared turn/hit predicates compare all2016/288contexts.',
            'T29 potion eligibility now uses actual impact UTC,with active/removed/after-launch/expired controls and once-only four-channel additions. Disconnected Spear/fan fixtures explicitly supply active frames without bypassing the production offline gate. Actual learned unpaid windup/once-only payment,locked-target interception exclusion,remaining125turn cadence,moving-target impact,dead locked target without reacquisition,ground initial10frames/lowest-ID acquisition,mode6extension/final expiry,seven lifecycle/slow controls and atomic malformed guidance rejection pass.',
            'Player usable mana and Pay cap latent fractional credit after native whole-mana regeneration reaches maximum,with17/255fraction controls and the retained Iron Golem bound. A broader1180marker/35save class regression covers the shared correction. Six connected right/quick casts use natural travel/payment. Two actual reloads preserve a turned locked mode1flight and an acquired mode5flight,including pose,vector,packet,target,seed,remaining/total age and deadlines. All202predecessor markers remain.'],
        remaining=['Current modern path/target validation,full generic dispatcher/hit-event composition and graphical-client behavior remain unverified. Source guidance probe supplies world/path/collision/stat/packet services explicitly. Native geometry,room/candidate/terrain/footprints,global frame/FCR and complete incoming/death/reaction/PvP/special recipients remain adapters.',
            'Native source-subtile projection,ground acquisition filter and vector recomputation reuse bounded services;self-ground zero-path,nonstandard target kinds,extreme slow/ground-frame domains,exact missing/dead/hostile-invalid path registry transitions and retired-owner credit still need acceptance. No invented blanket retargeting remains.',
            'Full class completion,deployment,old-content binary upgrade and final effects/audio/talent/hotkey icons are not claimed. Next:Poison Nova/Explosion flight/field allocation,then the remaining summon/curse audit.'])
    path=ROOT/'tools/reference/bone-spirit-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Bone Spirit:229markers/3boots/2reloads,4080source packets/2704guidance cases,6real inputs,1180broader markers/35saves,4audits/71Python;installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
