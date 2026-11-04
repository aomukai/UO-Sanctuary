#!/usr/bin/env python3
"""Bind recipient-scoped aura arbitration to the actual native regression."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/elite-aura-winner'
FOCUS=ROOT/'.local/class-skills/elite-aura-winner-focus'
PARENT='513b50aad87dc2a9f894ce350e5d90d704276db5'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    focused=inventory.validate_inventory(FOCUS/'birth-inputs.json')
    audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
        snapshot(STAGE/'pre-build-1.json',folder/name)
    assert native['artifacts']==focused['artifacts']==audit['artifacts']
    assert native['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/elite-amplify'
    prior=json.loads((previous/'birth-inputs.json').read_text())
    changed={n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}
    assert changed=={'src/Sanctuary.Content/EliteAuras.cs','tests/Sanctuary.Integration/EliteAuraWinnerChecks.cs'}
    assert native['copiedData']==prior['copiedData']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/elite-amplify-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log'
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==454
    expected['elite-aura-winner-check']+=3
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==457
    focus={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
           'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'elite-aura-winner-check':3}
    assert markers(FOCUS/'birth-native-checks.log')==focus
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text()
        assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    before=FOCUS/'before-fix';old=json.loads((before/'birth-inputs.json').read_text())
    assert 'Excluded-floor emitter suppressed eligible HolyFire damage pulse' in(before/'birth-native-checks.log').read_text()
    assert sha(before/'EliteAuras.cs')==prior['sourceInputs']['src/Sanctuary.Content/EliteAuras.cs']
    assert sha(before/'EliteAuraWinnerChecks.cs')==old['sourceInputs']['tests/Sanctuary.Integration/EliteAuraWinnerChecks.cs']
    assert sha(before/'integration/Sanctuary.Integration.dll')==old['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert old['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']
    for kind in ('content','integration'):
        assert all(t in(STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',992),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text()) and len(new['InputHashes'])==count
        assert new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for filename,digest in new['InputHashes'].items():assert sha(ROOT/filename)==digest,filename
        oldaudit=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items()if k not in skip}=={k:v for k,v in oldaudit.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,t in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),
                   ('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):
        assert t in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative
        assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not state['completedClasses']and sum(r['implementation']=='handler-present'for r in state['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'src/Sanctuary.Content/EliteAuras.cs',ROOT/'tests/Sanctuary.Integration/EliteAuraWinnerChecks.cs',
           *(ROOT/'tests'/n for n in ('check_elite_aura_winner.py','check_elite_aura_winner_focus.py')),
           *(ROOT/'docs'/n for n in ('elite-aura-winner-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
           *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json',
               'audit-native-checks.log','audit-run-1.log','content-build-1.log','integration-build-1.log','python-tests.log','python-before-audit-refresh.log',
               'readiness-check.log','catalogue-check.log','roadmap-check.log')),
           *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log')),
           *(p for p in sorted(before.rglob('*'))if p.is_file())]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Native aura recipient admission and deterministic highest-rank/lower-GUID arbitration;no new D2 mechanical rule',
        verified=dict(nativeMarkers=457,focusedMarkers=12,parentMarkersRetained=454,bootsPerCohort=3,actualReloadsPerCohort=2,
            auraKinds=['HolyFire','HolyFreeze','HolyShock'],scenariosPerKindPerBoot=5,damageWitness='Actual recipient OnDamage counts and original emitter identity',
            pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[992,21],handlers=191,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['This shares the existing native floor/range/LOS/visibility/allegiance adapter;it does not prove modern source geometry or aura/global event ordering.',
            'Converted recipient is qualified through the existing allegiance service;no conversion of an ineligible elite emitter is invented.',
            'The regression explicitly advances pulse inspection clocks;it is not natural-period or client-animation acceptance.',
            'No persistence layout or runtime-data change,new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    target=ROOT/'tools/reference/elite-aura-winner-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Aura winners:457broader/12focused markers;454parent retained;all3damage auras;protected build/saves unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
