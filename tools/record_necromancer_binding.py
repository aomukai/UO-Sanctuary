#!/usr/bin/env python3
"""Bind pinned Necromancer mouse policy, migrations and retained native checks."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/necromancer-binding'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    # The broad run finishes before a fixture-only correction. Its production
    # DLL/data must be identical; its integration DLL and pre-build inputs are
    # archived. PetChecks is not invoked by that broader runner.
    full=json.loads((STAGE/'class-inputs.json').read_text())
    pre=json.loads((STAGE/'pre-build-2.json').read_text())['inputs']
    full_cs={p:h for p,h in full['sourceInputs'].items() if p.endswith('.cs')}
    assert full_cs==pre['sourceInputs'] and full['copiedData']==pre['copiedData']==inputs['birth']['copiedData']
    assert sha(STAGE/'full-build-2/PetChecks.cs')==full_cs['tests/Sanctuary.Integration/PetChecks.cs']
    current_cs={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(full_cs)==set(current_cs)
    assert {p for p in full_cs if full_cs[p]!=current_cs[p]}=={'tests/Sanctuary.Integration/PetChecks.cs'}
    assert pre['projectInputs']==json.loads((STAGE/f'pre-build-{build}.json').read_text())['inputs']['projectInputs']
    for p,h in full['sourceInputs'].items():
        if not p.endswith('.cs'):assert sha(ROOT/p)==h,p
    assert 'pet-check' not in (ROOT/'tests/check_class_skills.py').read_text()
    for key,h in full['artifacts'].items():
        if key=='Assemblies/Sanctuary.Integration.dll':assert sha(STAGE/'full-build-2/Sanctuary.Integration.dll')==h
        else:assert h==inputs['birth']['artifacts'][key]
    inputs['class']=full
    previous=ROOT/'tools/reference/skeleton-birth-native-evidence.json'
    predecessor=json.loads(previous.read_text());ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    added=set(after)-set(before);changed={p for p in before if before[p]!=after[p]}
    assert added=={'tests/Sanctuary.Integration/NecromancerBindingChecks.cs'}
    assert changed=={'src/Sanctuary.Content/SkillBindings.cs',*['tests/Sanctuary.Integration/'+n+'.cs' for n in ['ClassHistoryChecks','FullWardrobeChecks','FullWardrobeUiChecks','PetChecks','PetUiChecks']]}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/skeleton-birth/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)
    for family in ['necromancer-binding','class-history','full-wardrobe']:
        expected+=Counter({family+'-check':1,family+'-reload-check':2})
    expected+=Counter({'pet-connected-check':1,'pet-connected-reload-check':1})
    assert sum(expected.values())==291 and markers(STAGE/'birth-native-checks.log')==expected
    for name,boots,saves in [('birth-native-checks.log',3,3),('class-native-checks.log',3,35)]:
        log=(STAGE/name).read_text();assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down')==boots and log.count('Writing world save snapshot done')==saves
    broad=ROOT/'.local/class-skills/bone-spirit/class-native-checks.log'
    prior=json.loads((ROOT/'tools/reference/bone-spirit-native-evidence.json').read_text())
    assert sha(broad)==prior['artifacts'][str(broad.relative_to(ROOT))]
    assert markers(STAGE/'class-native-checks.log')==markers(broad) and sum(markers(broad).values())==1180
    assert json.loads((STAGE/'necromancer-binding-fixtures.json').read_text())['Generation']==2
    reference=ROOT/'tools/reference/necromancer-binding-reference.json';proof=json.loads(reference.read_text())
    assert proof['referenceVersion']=='3.3.93847' and sha(ROOT/proof['source'])==proof['sourceSha256']
    assert len(proof['skills'])==30 and sum(x['left'] for x in proof['skills'])==4 and sum(x['right'] for x in proof['skills'])==27
    review=ROOT/'tools/reference/skeleton-components-review.json';reviewed=json.loads(review.read_text())
    for path,h in reviewed['inputs'].items():assert sha(ROOT/path)==h,path
    for item in reviewed['routines'].values():assert sha(ROOT/item['path'])==item['sha256']
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',831),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/skeleton-birth'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==size and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    log=(STAGE/'audit-native-checks.log').read_text();assert ' ERR]' not in log and ': failed' not in log and log.count('Shutting down')==1
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    paths=[previous,reference,review,ROOT/'tools/reference/skeleton-world-preflight.json',ROOT/'tools/prepare_skeleton_world_reference.py',ROOT/'docs/skeleton-components-source-review.md',Path(__file__).resolve(),ROOT/'tools/build_class_skills.py',broad,ROOT/'tools/reference/bone-spirit-native-evidence.json']
    paths += [ROOT/p for p in sorted(changed|added)]
    paths += [ROOT/p for p in ['tests/check_necromancer_binding.py','tests/check_iron_golem_birth.py','tests/check_pets_ui.py','docs/necromancer-binding-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','full-run-2.log','pre-build-2.json','content-build-2.log','integration-build-2.log','full-build-2/Sanctuary.Integration.dll','full-build-2/PetChecks.cs','birth-inputs.json','audit-inputs.json','class-inputs.json','birth-native-checks.log','audit-native-checks.log','class-native-checks.log','python-tests.log','necromancer-binding-fixtures.json','class-history-checks.json','full-wardrobe-checks.json','pet-check.json']]
    result=dict(referenceVersion='3.3.93847',component='Pinned Necromancer mouse bindings and legacy normalization',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=291,retainedPredecessorMarkers=280,boots=3,actualReloads=2,saves=3,fullRegressionMarkers=1180,fullRegressionSaves=35,fullRegressionBoots=3,fullRegressionReloads=2,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[831,21],unchangedInstalledArtifacts=106,sourceSkills=30,leftSkills=4,rightSkills=27,legacyMigrationContexts=8,connectedPetBoots=2,connectedPetReloads=1,savedGeneration=2,fullRegressionBuild=2,fullRegressionFixtureOnlyDelta='PetChecks.cs;not invoked by broad runner',handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Necromancer assignments and the server binding gump use the pinned left/right flags. Teeth,Poison Dagger,Bone Spear and Bone Spirit retain both mouse slots;other active Necromancer skills require right selection. Passive skills remain unavailable and quick selection retains its independent active-skill gate.',
            'Old invalid Necromancer left slots normalize to Basic Attack while valid right and quick slots stay intact. Both legacy binding versions,valid offensive left preservation,idempotent encoding and two actual reloads pass. Class-history and full-wardrobe restoration retain progression,ownership,equipment,pool ratios and quick slots.',
            'The retained historical pet kill fixture now follows real saved Teeth flights and controls the source seed to set exact fractional target life. It still requires exact-life death and34owner XP;release alone must leave the target alive. The fixture runs in a verified combat corridor. Online guard uses a real loopback socket with supplied authenticated-session context and asserts that the connection stays live,then detaches before offline persistence assertions. The historical-stat body has a separate rank-one owner so it cannot violate the main owner cap;its destructive owner-death reload is checked once. Earlier failed logs/input inventories are retained.',
            'All280predecessor focused markers and the exact1180-marker broader class Counter remain. The broader run uses frozen build2 with identical production content/kernel/data;its archived integration DLL differs only by the later PetChecks fixture correction,which its runner does not invoke. Final focused checks use the corrected fixture. Clean builds,4audits/71Python,573unchanged named decisions and106unchanged installed artifacts pass.'],
        remaining=['Graphical client binding art/layout and full current input/dispatcher/global-frame/FCR/PvP parity remain unverified. GUI fixture paths are updated but were not run in this increment. No class completion,deployment or binary save upgrade is claimed.',
            'Regular skeleton combat/AI/critical/action/equipment and golem/curse acceptance continue next. Complete-body static shield review shows the5percent caller flag can overwrite a randomized shield withnil at cosmetic level1;final current spawn distribution,regional pools,full-set modifier and owner/pet seed order remain uncertain. No invented shield probability was delivered.',
            'Effects/audio and talent/hotkey icons remain separate pending ledgers.'])
    target=ROOT/'tools/reference/necromancer-binding-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('Necromancer bindings:30pinned rows,291focused/1180broader markers,2reloads,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');args=p.parse_args();record(args.build,args.run,args.check)
