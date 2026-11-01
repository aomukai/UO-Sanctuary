#!/usr/bin/env python3
"""Record the existing Sorceress baseline without certifying learned input."""
import argparse
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-acceptance'
META=ROOT/'.local/class-skills/necromancer-roadmap-metadata'
sha=inventory.sha


def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-baseline-1.json',STAGE/'birth-inputs.json')
    parent=ROOT/'tools/reference/necromancer-roadmap-metadata-evidence.json'
    assert parent.read_bytes()==subprocess.check_output(['git','show','2dad29d:'+str(parent.relative_to(ROOT))],cwd=ROOT)
    reuse=json.loads((STAGE/'baseline-reuse.json').read_text());assert sha(parent)==reuse['metadataReceiptSha256']
    inherited=inventory.validate_inventory(META/'audit-inputs.json')
    assert tested['artifacts']==inherited['artifacts'] and tested['copiedData']==inherited['copiedData']
    assert {p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}=={p:h for p,h in inherited['sourceInputs'].items() if p.endswith('.cs')}
    log=(STAGE/'birth-native-checks.log').read_text();counts=markers(STAGE/'birth-native-checks.log')
    assert sum(counts.values())==88 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/'baseline-run-1.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_sorceress_acceptance import configuration
    config=configuration();assert len(config['extra_families'])==11 and len(config['extra_checks'])==5
    for family in config['extra_families']:
        assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
    for command in config['extra_checks']:assert counts[command]==3
    for name,size in (('named-item-runtime-audit.json',887),('first-loop-affix-audit.json',21)):
        exported=json.loads((ROOT/'tools/reference'/name).read_text())
        assert exported==json.loads((META/name).read_text()) and len(exported['InputHashes'])==size
        assert exported['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in exported['InputHashes'].items():assert sha(ROOT/path)==digest,path
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    sorc=[r for r in status['skills'] if r['characterClass']=='Sorceress']
    assert sum(r['implementation']=='handler-present' for r in sorc)==29 and not status['completedClasses']
    assert next(r for r in sorc if r['name']=='Inferno')['implementation']=='blocked'
    files=[parent,Path(__file__).resolve(),ROOT/'tests/check_sorceress_acceptance.py',
        ROOT/'docs/sorceress-acceptance-preflight.md',ROOT/'docs/sorceress-baseline-checkpoint.md',ROOT/'docs/class-skills-resume-checkpoint.md']
    files += [p for p in STAGE.iterdir() if p.is_file()]
    result=dict(referenceVersion='3.3.93847',component='Sorceress existing mechanics/save baseline',installed=False,
        gameplayChanged=False,learnedInfernoEnabled=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=88,boots=3,actualReloads=2,saves=3,fixtureFamilies=11,sharedChecks=5,
            sourceProbes=3,reusedCompiledBuild=10,sorceressHandlers=29,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Existing armor/shield/Enchant,ground fire/Meteor,Blizzard/Orb/Hydra,chain/storm/teleport and historical Inferno primitive states share three disposable saved worlds and two actual reloads.',
            'Charged Bolt,Lightning and prepared shared Inferno channel/sequence/rate checks reuse existing compiled bodies. All five assemblies,C# inputs and copied data match the accepted metadata audit;no rebuild or gameplay change is inferred from the new Python coordinator.'],
        remaining=['This baseline has no new real client input actions and does not establish all29learned skill contracts. Basic bolts/nova/static/mastery player-input,gear,payment,cancellation,lifecycle and final client acceptance remain explicit.',
            'The private Sorceress basic projectile adapter still uses Teeth range and live impact enhancement;compare its capture/motion/save/ReturnFire contracts with existing shared missile services before changing it.',
            'Historical Inferno primitive FCR assertions are not learned channel-rate proof. Modern accepted input/payment/release remains individually deferred;effects/audio and talent/hotkey icons stay separately tracked.'])
    output=ROOT/'tools/reference/sorceress-baseline-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Sorceress baseline:88markers/3boots/2reloads/3saves;29handlers remain incompletely verified;Inferno stays deferred.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
