#!/usr/bin/env python3
"""Bind compiler research to the unchanged accepted production checkpoint."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
PRODUCTION = '524328631d6b8eae7e2e040660bf0dfc34ffa11e'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    stage = ROOT / '.local/class-skills/modern-passive-compiler'
    proofpath = ROOT / 'tools/reference/modern-passive-compiler-evidence.json'
    proof = json.loads(proofpath.read_text())
    assert proof['referenceVersion'] == '3.3.93847'
    assert (proof['passiveSlotsExamined'], proof['compiledFormulas'], proof['byteExactFormulas'], proof['constantFoldNumericAnalogueCases']) == (5148, 229, 229, 5355)
    assert proof['rawDefilerCompiledExactly'] and proof['constantFoldResult'] == 'rank-1+hardMastery'
    assert not any(proof[k] for k in ('currentFormulaExecutionVerified', 'productionChanged', 'handlerEnabled'))
    for name, digest in {**proof['inputs'], **proof['artifacts']}.items():
        assert sha(ROOT / name) == digest, name
    snapshot(ROOT / '.local/class-skills/defiler-body/pre-build-1.json')
    previous = json.loads(subprocess.check_output(['git', 'show', PRODUCTION + ':tools/reference/defiler-body-native-evidence.json'], cwd=ROOT))
    assert (previous['verified']['nativeMarkers'], previous['verified']['focusedNativeMarkers']) == (393, 61)
    for name, digest in previous['tested']['artifacts'].items():
        relative = ('native/Distribution/' + name if name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll')
                    else 'content/Sanctuary.Content.dll' if name == 'Assemblies/Sanctuary.Content.dll'
                    else 'integration/Sanctuary.Integration.dll')
        assert sha(ROOT / '.local/class-skills/defiler-body' / relative) == digest, name
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        path = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count
        assert {str(p.relative_to(path)): sha(p) for p in path.rglob('*') if p.is_file()} == protected[key]
    assert 'dynamic runtime execution remains unverified' in (stage / 'source-check.log').read_text()
    files = [Path(__file__).resolve(), proofpath, stage / 'source-check.log',
             *(ROOT / 'docs' / name for name in ('modern-passive-compiler-checkpoint.md', 'defiler-compiled-formula-checkpoint.md',
                                                'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md'))]
    result = dict(referenceVersion='3.3.93847', productionCheckpoint=PRODUCTION,
                  component='Modern passive compiler equivalence research', productionChanged=False,
                  handlerEnabled=False, goalComplete=False,
                  verified=dict(byteExactPassiveFormulas=229, numericAnalogueControls=5355,
                                dynamicRuntimeExecution=False, unchangedProductionBuild=True,
                                retainedProductionMarkers=393, retainedFocusedMarkers=61,
                                protectedInstalledArtifactsUnchanged=106, protectedSavesUnchanged=38,
                                protectedConfigurationsUnchanged=20),
                  limitations=[
                      'No new native run is claimed:production C#,runtime data and five accepted assemblies are unchanged.',
                      'Raw compilation and numeric constant folding do not establish dynamic current-game execution. Defiler damage,Health Link and learned handler remain disabled;continue unaffected work rather than inventing the unresolved runtime behavior.',
                  ], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/modern-passive-compiler-boundary-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Modern compiler boundary:229 byte-exact formulas/5355 numeric controls;production393/61 and106 installed/38 save/20 configuration files unchanged;dynamic damage still gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
