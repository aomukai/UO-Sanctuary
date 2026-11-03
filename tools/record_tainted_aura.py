#!/usr/bin/env python3
"""Bind Tainted NPC420 aura source, native/reload and preservation evidence."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/tainted-aura'
FOCUS=ROOT/'.local/class-skills/tainted-aura-focus'
PARENT='68242b86632b798f254cf43e9914ed129f3dbd81'
sha=inventory.sha


def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    focused=inventory.validate_inventory(FOCUS/'birth-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(STAGE,'audit-inputs.json'),(FOCUS,'birth-inputs.json')):
        snapshot(STAGE/'pre-build-3.json',folder/name)
    assert native['artifacts']==audit['artifacts']==focused['artifacts']
    assert native['copiedData']==audit['copiedData']==focused['copiedData']
    prior=json.loads((ROOT/'.local/class-skills/tainted-ranks/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/PaladinAuras.cs','src/Sanctuary.Content/TaintedPrepared.cs','src/Sanctuary.Content/WarlockTaintedBody.cs',
        'src/Sanctuary.Content/TaintedResistFire.cs','tests/Sanctuary.Integration/TaintedAuraChecks.cs'}
    assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    # Both failed native runs were test-placement/training fixtures; content DLLs retained exactly.
    for diagnostic in (1,2):
        assert sha(STAGE/f'diagnostic-{diagnostic}/content/Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert ': failed' in (STAGE/f'diagnostic-{diagnostic}/birth-native-checks.log').read_text()
    proof_path=ROOT/'tools/reference/tainted-aura-source-evidence.json';proof=json.loads(proof_path.read_text())
    assert proof['referenceVersion']=='3.3.93847' and not proof['currentExecutableParity'] and not proof['handlerEnabled']
    assert (proof['fullCallbackFactoryModifierCases'],proof['fullDo65FilterIteratorCases'])==(1152,770)
    for path,digest in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/path)==digest,path
    baseline=ROOT/'.local/class-skills/tainted-ranks/birth-native-checks.log'
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/tainted-ranks-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['tainted-aura-check']+=1;expected['tainted-aura-reload-check']+=2;expected['tainted-aura-reload-result']+=2
    assert sum(expected.values())==387 and markers(STAGE/'birth-native-checks.log')==expected
    focused_expected=markers(ROOT/'.local/class-skills/tainted-ranks-focus/birth-native-checks.log')
    for name,count in (('tainted-aura-check',1),('tainted-aura-reload-check',2),('tainted-aura-reload-result',2)):focused_expected[name]+=count
    assert markers(FOCUS/'birth-native-checks.log')==focused_expected and sum(focused_expected.values())==55
    for folder,count in ((STAGE,387),(FOCUS,55)):
        log=(folder/'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==count
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('tainted-aura-reload-result: passed')==2
        state=json.loads((folder/'tainted-aura-fixtures.json').read_text())
        assert state['Generation']==2 and state['Body']!=state['Owner'] and state['Helm']>0
        assert state['Mana']==22784 and state['Life']==116786 and state['Emitter']['Actor']==state['Body']
        body=json.loads(state['BodyState']);assert body['Owner']==state['Owner'] and body['Profile']=='prepared-tainted-life-owner-v1'
        assert len(state['Leases'])==2 and {s['Target'] for s in state['Leases']}=={state['Owner'],state['Body']}
        assert all(s['Rank']==2 and s['Percent']==29 and s['SourceSkill']==420 and s['MaximumResistance']==0 and s['Source']==state['Body'] for s in state['Leases'])
    for kind in ('content','integration'):
        assert all(t in (STAGE/f'{kind}-build-3.log').read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',980),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text()) and len(current['InputHashes'])==count
        assert current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    assert (STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),
                      ('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.'),('source-check.log','1152 complete callback/factory/modifier and770 do65/filter cases')):
        assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    preserved=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative in (('saves','.local/server/Saves'),('configuration','.local/server/Configuration')):
        folder=Path(installed['runtimeRoot'])/relative;assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==preserved[key]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    files=[Path(__file__).resolve(),proof_path,ROOT/'tools/probe_tainted_aura.py',ROOT/'tools/build_skill_roadmap.py',baseline,
        ROOT/'tests/check_tainted_aura.py',ROOT/'tests/check_tainted_aura_focus.py',
        *(ROOT/'docs'/n for n in ('tainted-aura-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-3.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log',
                           'content-build-3.log','integration-build-3.log','source-check.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','tainted-aura-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','tainted-aura-fixtures.json'))]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,component='Tainted inherent NPC420 resistance selection and periodic leases',handlerEnabled=False,goalComplete=False,completedClasses=[],
        verified=dict(nativeMarkers=387,retainedNativeMarkers=382,focusedNativeMarkers=55,boots=3,reloads=2,saves=3,fullCallbackFactoryModifierCases=1152,fullDo65FilterIteratorCases=770,
                      nativeNpcCallbackProjections=576,nativeDo65FormulaProjections=770,originalActorPulseAndParkingExpiryReloads=2,pythonTests=71,namedDecisions=573,auditFingerprints=[980,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        tested=native,limitations=[
            'Prepared component only. GenericPet world/controller/owner/mode admission,complete skill-list/extended-passive services,Blood Boil,incoming combat,learned payment/shared cap and full lifecycle remain unfinished.',
            'The source callback composition has no passive list and records rather than invokes removal callbacks; complete passive-list restoration history,current executable/global scheduler/order/geometry/client parity are not certified.',
            'Saved emitter/recovery/recipient deadlines are explicitly held. After both reloads the original emitter is rearmed and real timers pulse then expire finite leases while parked,without resource recovery. This is not unheld downtime proof.',
            'Recovery preserved reboot outputs before rerunning. Three source diagnostics cover Python name shadowing,a missing fixture include and a mistaken absence-of-passivestate assertion. Two native diagnostics cover occupied placement and training while overlapping an ally. Production content DLL is identical across all three native builds.',
            'No deployment or installed-save write,new learned handler,class completion or final assets. Generic aura marker and summon appearance are identified placeholders;effects/audio and talent/right/quick icons remain separate.'],
        evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    path=ROOT/'tools/reference/tainted-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Tainted aura:387 retained/55 focused checks;1152 callback+770 do65 source cases;two original-emitter pulse/parking-expiry reloads;installed build/saves preserved.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
