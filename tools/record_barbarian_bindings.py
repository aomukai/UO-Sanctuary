#!/usr/bin/env python3
"""Record the bounded Barbarian mouse-policy/save regression acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
from build_barbarian_bindings import build

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/barbarian-bindings'
PARENT='f13bd695cf2bc553d30f750ee56af52a276cd5ed'
sha=inventory.sha

def historical(path):
    return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)

def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    reference=ROOT/'tools/reference/barbarian-binding-reference.json'
    assert json.loads(reference.read_text())==build()
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==21
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ('barbarian-binding','necromancer-binding','sorceress-binding','paladin-binding','iron-golem-items','npc-resource-callbacks'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    assert log.count('iron-golem-birth-check: passed')==3
    assert '132legacy migrations/idempotence and history' in log
    fixture=json.loads((STAGE/'barbarian-binding-fixtures.json').read_text())
    assert fixture['Generation']==2 and fixture['Owner']>0
    prior=json.loads((ROOT/'.local/class-skills/paladin-aura-persistence/birth-inputs.json').read_text())
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and hashlib.sha256(historical(p)).hexdigest()!=h}
    assert changed=={'src/Sanctuary.Content/SkillBindings.cs'}
    assert tested['copiedData']==prior['copiedData']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert tested['artifacts'][name]==prior['artifacts'][name]
    for part in ('content','integration'):
        text=(STAGE/(part+'-build-2.log')).read_text()
        assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    negative=STAGE/'negative-parent-1'
    assert 'barbarian-binding-check: failed' in (negative/'birth-native-checks.log').read_text()
    assert 'Legacy Barbarian normalization lost valid slot/retained forbidden slot' in (negative/'birth-native-checks.log').read_text()
    assert sha(negative/'content/Sanctuary.Content.dll')==prior['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert (negative/'SkillBindings.cs').read_bytes()==historical('src/Sanctuary.Content/SkillBindings.cs')
    diagnostic=STAGE/'diagnostic-missing-backpack-1'
    old=(diagnostic/'BarbarianBindingChecks.cs').read_text()
    current=(ROOT/'tests/Sanctuary.Integration/BarbarianBindingChecks.cs').read_text()
    assert current.replace('p.AddItem(new Server.Items.Backpack());','')==old
    assert (negative/'BarbarianBindingChecks.cs').read_text()==old
    assert 'Barbarian history exit' in (diagnostic/'birth-native-checks.log').read_text()
    assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
    for archive in (negative,diagnostic):
        inputs=json.loads((archive/'birth-inputs.json').read_text())
        assert sha(archive/'BarbarianBindingChecks.cs')==inputs['sourceInputs']['tests/Sanctuary.Integration/BarbarianBindingChecks.cs']
        assert sha(archive/'integration/Sanctuary.Integration.dll')==inputs['artifacts']['Assemblies/Sanctuary.Integration.dll']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert {k:v for k,v in tested['sourceInputs'].items() if k.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in (('named-item-runtime-audit.json',911),('first-loop-affix-audit.json',21)):
        data=json.loads((STAGE/name).read_text());assert data==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(data['InputHashes'])==count and data['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,h in data['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in data.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    text=(STAGE/'catalogue-checks.log').read_text()
    assert all(s in text for s in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Barbarian:30pinned mouse rows'))
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
    files=[Path(__file__).resolve(),reference,ROOT/'tools/build_barbarian_bindings.py',ROOT/'tests/check_barbarian_bindings.py',
        *(STAGE/n for n in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','barbarian-binding-fixtures.json','content-build-2.log','integration-build-2.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','catalogue-checks.log')),
        *(ROOT/'docs'/n for n in ('barbarian-binding-checkpoint.md','barbarian-class-roadmap-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    for archive in (negative,diagnostic):
        files.extend(archive/n for n in ('birth-inputs.json','birth-native-checks.log','BarbarianBindingChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'))
    data=dict(referenceVersion='3.3.93847',parent=PARENT,scope='Barbarian pinned mouse flags and saved normalization; no complete class or deployment.',
        verified=dict(pinnedBarbarianRows=30,migrationContexts=132,nativeCommandMarkers=21,boots=3,reloads=2,saves=3,auditMarkers=2,pythonTests=71,unchangedInstalledArtifacts=106),
        caveats=['Deferred Double Swing/Frenzy/Leap Attack/Grim Ward remain unavailable. Valid saved references and independent quick slots are retained.',
            'Parent regression uses the original fixture; final fixture only adds the backpack required by history switching. Diagnostic content DLL equals accepted content DLL.',
            'No new combat formulas, packet-route/client-rendering claim, class certificate, assets, installed build or save mutation.'],
        tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/barbarian-binding-native-evidence.json';serialized=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==serialized
    else:out.write_text(serialized)
    print('Barbarian bindings:30pinned rows/132migrations;four-class21markers/3boots/2reloads/3saves;parent regression;71Python/2audits/106installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
