#!/usr/bin/env python3
"""Bind original-world migration and corrected client packaging evidence."""
import argparse
import json
from pathlib import Path
import re
import skill_checkpoint_evidence as e
from prepare_class_skills_handoff import hashes, identities, protected, selected_files

ROOT = e.ROOT
WORLD = ROOT / '.local/class-skills/class-skills-handoff'
CLIENT = ROOT / '.local/class-skills/class-skills-client-package'


def diagnostics(text):
    return {re.sub(r'^.*?/upstream/ClassicUO/', '', line.split(' [', 1)[0])
            for line in text.splitlines() if re.search(r'warning (CS|CA)\d+:', line)}


def record(check=False):
    prepared = e.load(WORLD / 'prepared-inputs.json')
    proof = e.load(WORLD / 'world-smoke.json')
    classes = e.load(ROOT / 'tools/reference/warlock-lifecycle-acceptance-native-evidence.json')
    assert prepared['classReceiptSha256'] == e.sha(ROOT / 'tools/reference/warlock-lifecycle-acceptance-native-evidence.json')
    assert prepared['artifacts'] == classes['artifacts'] and classes['nativeMarkers'] == 906
    assert prepared['handlers'] == 240 and prepared['clientPatches'] == 44
    for path, digest in prepared['candidateServer'].items():
        assert e.sha(WORLD / 'server' / path) == digest, path
    assert json.loads((WORLD / 'server/Data/assemblies.json').read_text()) == ['UOContent.dll', 'Sanctuary.Content.dll']
    assert not (WORLD / 'server/Assemblies/Sanctuary.Integration.dll').exists()
    assert (proof['boots'], proof['saves'], proof['reloads']) == (3, 3, 2)
    assert proof['originalPlayers'] == proof['originalSkeletonGuardians'] == 2
    assert proof['identitiesPreserved'] and proof['originalRuntimeUnchanged'] and not proof['installed']
    for snapshot in proof['snapshots']:
        assert snapshot['originalMobileIdentities'] == snapshot['mobileCount'] == 572
        assert snapshot['originalItemIdentities'] == 4698
    for kind, count in (('Mobiles', 572), ('Items', 4698)):
        relative = Path('Saves') / kind / (kind + '.idx')
        original = identities(WORLD / 'original/.local/server' / relative)
        assert identities(WORLD / 'server' / relative) == original and len(original) == count
    text = (WORLD / 'world-smoke.log').read_text()
    assert text.count('Loading world done (4698 items, 572 mobiles)') == 3
    assert text.count('Writing world save snapshot done') == text.count('Shutting down') == 3
    assert ' ERR]' not in text and ': failed' not in text and 'create the owner account now?' not in text
    runtime = Path(prepared['runtimeRoot'])
    assert protected(runtime) == prepared['original'] == protected(WORLD / 'original')
    assert selected_files(runtime) == prepared['originalFiles']
    for path, digest in prepared['originalFiles'].items():
        assert e.sha(WORLD / 'original' / path) == digest
    protection = e.protection('tools/reference/consume-learned-client-evidence.json', 44)
    fresh = e.load(CLIENT / 'source-inputs-1.json')
    assert fresh['qualifiedManifestSha256'] == e.sha(ROOT / '.local/class-skills/consume-learned/client-inputs-1.json')
    assert fresh['freshRestore'] and fresh['excludedStaleDirectories'] == ['obj', 'bin']
    for path, digest in fresh['allProjectSources'].items():
        assert e.sha(CLIENT / 'source' / path) == digest, path
    for path, digest in fresh['sharedInputs'].items():
        assert e.sha(ROOT / path) == e.sha(CLIENT / 'source' / path) == digest
    for path, digest in fresh['patchHashes'].items():
        assert e.sha(ROOT / path) == digest
    assert len(fresh['patchHashes']) == 44
    build = e.load(CLIENT / 'build-result-1.json')
    assert build['exitCode'] == 0
    for path, digest in build['artifacts'].items():
        assert e.sha(CLIENT / 'client' / path) == digest
    new = (CLIENT / 'build-1.log').read_text()
    old = (ROOT / '.local/class-skills/consume-learned/client-build-1.log').read_text()
    assert 'Build succeeded.' in new and '0 Error(s)' in new and diagnostics(new) == diagnostics(old)
    deps = e.load(CLIENT / 'client/cuo.deps.json')
    libraries = next(iter(deps['targets'].values()))
    required = ('ClassicUO.Utility', 'ClassicUO.IO', 'ClassicUO.Assets', 'ClassicUO.Renderer')
    assert all(any(name.startswith(needed + '/') for name in libraries) for needed in required)
    startup = e.load(CLIENT / 'client-startup.json')
    assert startup['aliveAfterRender'] and startup['headless'] and not startup['loggedIn'] and not startup['gameplayVerified']
    assert startup['clientSha256'] == e.sha(CLIENT / 'client/cuo.dll')
    assert startup['screenshotSha256'] == e.sha(CLIENT / 'client-login.png')
    assert 'Files loaded in:' in (CLIENT / 'client-startup.log').read_text()
    ledger = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert len(ledger['skills']) == 240 and all(row['implementation'] == 'handler-present' for row in ledger['skills'])
    paths = ['tools/prepare_class_skills_handoff.py', 'tools/prepare_class_client_package.py',
             'tests/check_class_package_client.py', 'tools/record_class_skills_handoff.py',
             'docs/class-skills-handoff-checkpoint.md', 'docs/class-skill-roadmap.md',
             'docs/class-skill-status-overview.md', 'README.md',
             'tools/reference/warlock-lifecycle-acceptance-native-evidence.json',
             'tools/reference/consume-learned-client-evidence.json']
    files = [*(ROOT / path for path in paths), *(WORLD / name for name in (
        'prepared-inputs.json', 'prepare-and-smoke-source-1.py', 'world-smoke.json', 'world-smoke.log', 'smoke-run-1.log',
        'failed-client-1/driver.py', 'failed-client-1/client-startup.log', 'failed-client-1/client-run-1.log',
        'failed-client-1/artifacts.json', 'failed-client-1/client-package.tar.gz')),
        *(CLIENT / name for name in ('source-inputs-1.json', 'build-result-1.json', 'build-1.log',
            'client-smoke-source-1.py', 'client-startup.json', 'client-startup.log', 'client-login.png', 'client-run-1.log')),
        *(ROOT / '.local/class-skills/class-skills-handoff-preparation-failure' / name
          for name in ('failed-preparation-script.py', 'failed-preparation.json'))]
    e.write('class-skills-handoff-native-evidence.json', dict(referenceVersion='3.3.93847',
        component='Current240-handler package, original-world persistence and corrected44patch client dependencies',
        handlers=240, classes=8, classArtifacts=classes['artifacts'], originalWorldBoots=3,
        originalWorldSaves=3, originalWorldReloads=2, originalPlayers=2, originalSkeletonGuardians=2,
        originalMobileIdentities=572, originalItemIdentities=4698, installed=False,
        installedArtifactsChanged=False, runtimeDataChanged=False, serverSourceChanged=False,
        clientSourceChanged=False, clientPatches=44, clientBuildErrors=0,
        clientBuildWarnings=int(re.findall(r'(\d+) Warning\(s\)', new)[-1]),
        originalClientBuildWarnings=44, sourceDiagnosticSetUnchanged=True,
        clientDependencyGraphFixed=True, clientStartupRendered=True, graphicalGameplayVerified=False,
        clientArtifacts=build['artifacts'], protection=protection, goalComplete=False,
        limits='Disposable copy loads and saves the original world three times,retaining every mobile/item serial,type and creation identity;actual installed38save files,configuration,client and old build remain unchanged. Initial missing optional Profiles directory halted preparation before native startup. The former qualified client compiled,but inherited stale project-reference assets and a deps file omitting all four ClassicUO libraries. Its exact failing package,driver and logs are frozen. A fresh restore/build of unchanged qualified source/44patches fixes the runtime dependency graph;50warnings include the unchanged source diagnostic set and restored package diagnostics. Xvfb login rendering is inspected,with no account login or gameplay claim. Promotion,matching source/staging-cache synchronization and branch consolidation remain required.'), files, check)
    print('Prepared240-handler handoff:572mobiles/4698items unchanged through3saves/2reloads;fresh44patch client renders;real installation unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
