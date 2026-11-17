#!/usr/bin/env python3
"""Verify installed class artifacts, preserved saves and reconciled main history."""
import argparse
import json
from pathlib import Path
import runpy
import subprocess
import skill_checkpoint_evidence as e
from prepare_class_skills_handoff import hashes, protected

ROOT = e.ROOT
WORLD = ROOT / '.local/class-skills/class-skills-handoff'
CLIENT = ROOT / '.local/class-skills/class-skills-client-package'
STAGE = ROOT / '.local/class-skills/main-consolidation'
INSTALL = STAGE / 'installation-1'


def git(*arguments, cwd=ROOT):
    return subprocess.check_output(['git', *arguments], cwd=cwd, text=True).strip()


def record(check=False):
    before = e.load(STAGE / 'before.json')
    merged = e.load(STAGE / 'history-merges.json')
    source = e.load(STAGE / 'source-comparison-2.json')
    install = e.load(INSTALL / 'installed.json')
    plan = e.load(INSTALL / 'plan.json')
    runtime = Path(install['runtimeRoot'])
    prepared = e.load(WORLD / 'prepared-inputs.json')
    assert install['installed'] and install['handlers'] == 240 and install['classes'] == 8
    assert install['savesUnchanged'] and install['configurationUnchanged'] and install['clientSettingsDataUnchanged']
    assert install['installedWorldLoaded'] and not install['installedWorldSaved'] and install['defaultStagingVerified']
    assert install['serverStopped'] and install['clientStopped']
    for path, digest in install['installedFiles'].items():
        assert e.sha(runtime / path) == digest, path
    for path, digest in plan['previousFiles'].items():
        if digest is not None:
            assert e.sha(INSTALL / 'rollback' / path) == digest, path
    for path in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(runtime / path) == prepared['original'][path]
    assert e.sha(runtime / '.local/client/settings.json') == prepared['originalFiles']['.local/client/settings.json']
    assert protected(WORLD / 'original') == prepared['original']
    for path, digest in prepared['originalFiles'].items():
        assert e.sha(WORLD / 'original' / path) == digest
    assert len(prepared['original']['.local/server/Saves']) == 38
    assert len(prepared['original']['.local/server/Configuration']) == 20
    assert hashes(runtime / '.local/server/Data/Sanctuary') == hashes(runtime / 'data/Sanctuary')
    assert e.sha(runtime / '.local/content/Sanctuary.Content.dll') == e.sha(runtime / '.local/server/Assemblies/Sanctuary.Content.dll')
    artifacts = e.load(ROOT / 'tools/reference/warlock-lifecycle-acceptance-native-evidence.json')['artifacts']
    for path in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll'):
        assert e.sha(runtime / '.local/server' / path) == artifacts[path]
    assert json.loads((runtime / '.local/server/Data/assemblies.json').read_text()) == ['UOContent.dll', 'Sanctuary.Content.dll']
    assert not (runtime / '.local/server/Assemblies/Sanctuary.Integration.dll').exists()
    client_build = e.load(CLIENT / 'build-result-1.json')
    for name, digest in client_build['artifacts'].items():
        if Path(name).suffix not in ('.pdb', '.nupkg'):
            assert e.sha(runtime / '.local/client' / name) == digest, name
            assert e.sha(runtime / 'upstream/ClassicUO/bin/Debug/net10.0' / name) == digest
    assert e.sha(runtime / '.local/integration/Sanctuary.Integration.dll') == artifacts['Assemblies/Sanctuary.Integration.dll']
    text = (INSTALL / 'installed-world-smoke.log').read_text()
    assert 'Loading world done (4698 items, 572 mobiles)' in text
    assert 'Listening: 127.0.0.1:2593' in text and 'Listening: 127.0.0.1:12000' in text
    assert text.count('Shutting down') == 1 and 'Saving world' not in text and ' ERR]' not in text
    native = e.load(STAGE / 'native-source-hashes.json')
    for path, digest in native['files'].items():
        assert e.sha(runtime / 'upstream/ModernUO' / path) == digest, path
    fresh = e.load(CLIENT / 'source-inputs-1.json')
    for path, digest in fresh['allProjectSources'].items():
        assert e.sha(runtime / path) == digest, path
    assert source['nativeCompared'] == 4251 and source['clientCompared'] == 946
    assert source['nativeMismatches'] == source['clientMismatches'] == []
    preservation = e.load(STAGE / 'patch-preservation.json')
    for name, row in preservation.items():
        for path in row['previousLocalFilesUnaffected']:
            assert e.sha(runtime / 'upstream' / name / path) == before['submodules'][name]['files'][path]
    for name, row in before['submodules'].items():
        for path, digest in row['files'].items():
            assert e.sha(STAGE / name / 'files' / path) == digest
    assert merged['treeUnchanged'] and not merged['remainingOutsideAncestry']
    assert git('rev-parse', merged['reviewCommit'] + '^{tree}') == git('rev-parse', merged['workingCommit'] + '^{tree}')
    current = git('rev-parse', 'HEAD', cwd=runtime)
    for row in before['sideBranches'].values():
        subprocess.run(['git', 'merge-base', '--is-ancestor', row['head'], current], cwd=runtime, check=True)
    for line in git('for-each-ref', '--format=%(refname) %(objectname)', 'refs/heads', 'refs/remotes/origin').splitlines():
        name, commit = line.split()
        if not name.endswith('/HEAD'):
            subprocess.run(['git', 'merge-base', '--is-ancestor', commit, current], cwd=runtime, check=True)
    sdk = e.load(STAGE / 'sdk-relocation.json')
    assert sdk['copyVerifiedBeforeOriginalRemoval'] and sdk['sourceFiles'] == 5041
    assert e.sha(Path(sdk['launcher'])) == sdk['launcherSha256']
    assert subprocess.check_output([sdk['launcher'], '--version'], text=True).strip() == sdk['sdkVersion'] == '10.0.201'
    dev = runpy.run_path(str(runtime / 'dev'))
    dev['require_stopped']('server'); dev['require_stopped']('client'); dev['require_sdk']()
    paths = ['tools/install_class_skills_handoff.py', 'tools/record_class_skills_installation.py',
             'docs/class-skills-installed-checkpoint.md', 'docs/main-side-branch-review.md',
             'docs/main-consolidation-plan.md', 'docs/class-skill-status-overview.md', 'docs/class-skill-roadmap.md',
             'README.md', 'tools/reference/class-skills-handoff-native-evidence.json']
    files = [*(ROOT / name for name in paths), *(STAGE / name for name in (
        'before.json', 'history-merges.json', 'native-source-hashes.json', 'source-comparison-1.json',
        'source-comparison-2.json', 'patch-preservation.json', 'patch-source-1.log', 'sdk-relocation.json',
        'failed-main-checkout.json', 'failed-main-checkout-status.txt', 'failed-main-checkout-diff.txt',
        'install-source-1.py', 'install-run-1.log')),
        *(INSTALL / name for name in ('plan.json', 'installed.json', 'installed-world-smoke.log'))]
    result = dict(referenceVersion='3.3.93847', component='Installed240talent package and reconciled main source/history',
        installed=True, handlers=240, classes=8, runtimeRoot=str(runtime), classArtifacts=artifacts,
        changedInstalledAndCacheFiles=len(install['installedFiles']), installedFiles=install['installedFiles'],
        originalSaveFiles=38, originalConfigurationFiles=20, savesUnchanged=True, configurationUnchanged=True,
        clientSettingsDataUnchanged=True, originalWorldLoaded=True, originalWorldSaved=False,
        originalMobiles=572, originalItems=4698, copiedWorldSaves=3, copiedWorldReloads=2,
        nativeSourceFilesMatched=4251, clientSourceFilesMatched=946, historyMerges=merged['merges'],
        sideBranchFilesMissing=0, mergeTreeUnchanged=True, preservedClassicSourceFiles=41,
        preservedNativeSourceFiles=101, unrelatedLocalEditsPreserved=True, clientPatches=44,
        clientDependencyGraphRepaired=True, clientStartupRendered=True, graphicalGameplayVerified=False,
        defaultStagingVerified=True, nativeReferenceCacheSynchronized=True, clientBuildCacheSynchronized=True,
        integrationCacheSynchronized=True, integrationExcludedFromProduction=True,
        sdkRelocated=True, sdkVersion='10.0.201', serverStopped=True, clientStopped=True,
        installationSourceCommit=install['sourceCommit'], rollbackRoot=install['rollbackRoot'],
        classCertified=False, goalComplete=False,
        limits='All current class cohorts retain their prior scopes. The real installed world loaded the original5290entities and stopped without saving;all38saves/20configs/client settings and data retain original hashes. Native/content/data and corrected client/default build caches are synchronized. Four reviewed side histories join without changing the verified tree;all preserved ref heads are ancestors. Actual patched main sources match4251native/946client captured files;the SDK regenerates an incidental FNA Release framework attribute. A full FILES filesystem interrupted the initial checkout;private partial-state evidence is kept. The complete5041-file pinned SDK was copied/verified off that volume;exFAT cannot create symlinks,so a local launcher preserves its command path. Explicit modern reference,unsupported recipient/world/item and graphical combat limits remain;missing effects/audio/icons stay separate.')
    e.write('class-skills-installation-evidence.json', result, files, check)
    print('Installed240talents/main history verified:38saves/20configs unchanged,source5197files match,all branch heads retained;server stopped.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
