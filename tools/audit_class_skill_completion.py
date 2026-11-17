#!/usr/bin/env python3
"""Audit all talent identities, preserved native/input evidence and unresolved scope."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from prepare_class_skills_handoff import hashes

ROOT = e.ROOT
CLASSES = ('necromancer', 'sorceress', 'paladin', 'barbarian', 'amazon', 'assassin', 'druid', 'warlock')
HELPER = 'tools/class_input_coverage.py'


def historical_helper(digest):
    for commit in subprocess.check_output(['git', 'log', '--all', '--format=%H', '--', HELPER], cwd=ROOT, text=True).splitlines():
        content = subprocess.check_output(['git', 'show', commit + ':' + HELPER], cwd=ROOT)
        if hashlib.sha256(content).hexdigest() == digest:
            return commit
    raise AssertionError('Missing original input-audit helper: ' + digest)


def audit(check=False):
    ledger_path = ROOT / 'data/Sanctuary/Balance/class-skill-status.json'
    ledger = e.load(ledger_path)
    assert ledger['referenceVersion'] == '3.3.93847'
    assert not ledger['contentScope']['charms'] and not ledger['contentScope']['runewords']
    rows = {r['talentId']: r for r in ledger['skills']}
    assert len(rows) == len(ledger['skills']) == len({r['sourceId'] for r in rows.values()}) == 240
    assets = {name: e.load(ROOT / ('data/Sanctuary/Balance/' + name + '.json'))
              for name in ('skill-effects', 'skill-icons')}
    for name, register in assets.items():
        assert len(register) == 240 and {r['talentId'] for r in register} == set(rows), name
        assert all(r['placeholder'] for r in register), name
    installed = e.load(ROOT / 'tools/reference/class-skills-installation-evidence.json')
    current = e.load(ROOT / 'tools/reference/warlock-lifecycle-acceptance-native-evidence.json')['artifacts']
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    install_path = ROOT / '.local/class-skills/main-consolidation/installation-1/installed.json'
    assert e.sha(install_path) == installed['files'][str(install_path.relative_to(ROOT))]
    stage_install = e.load(install_path)
    assert stage_install['installed'] and stage_install['handlers'] == 240
    for name, digest in stage_install['installedFiles'].items():
        assert e.sha(runtime / name) == digest, name
    original_path = ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json'
    original = e.load(original_path)
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(runtime / name) == original['original'][name], name
    assert e.sha(runtime / '.local/client/settings.json') == original['originalFiles']['.local/client/settings.json']
    bridge_path = ROOT / 'tools/reference/sentinel-stealskull-link-native-evidence.json'
    bridge = e.load(bridge_path)
    sentinel = 'src/Sanctuary.Content/BladeSentinelMissiles.cs'
    assert bridge['artifacts']['Assemblies/Sanctuary.Content.dll'] == current['Assemblies/Sanctuary.Content.dll']
    assert e.sha(ROOT / sentinel) == bridge['files'][sentinel]
    original_sentinel = hashlib.sha256(subprocess.check_output(
        ['git', 'show', bridge['baselineCommit'] + ':' + sentinel], cwd=ROOT)).hexdigest()
    classes, talents, files = [], [], [ledger_path, bridge_path]
    for name in CLASSES:
        input_path = ROOT / ('tools/reference/' + name + '-current-input-coverage.json')
        receipt_path = ROOT / ('tools/reference/' + name + '-lifecycle-acceptance-native-evidence.json')
        inputs, receipt = e.load(input_path), e.load(receipt_path)
        stage = ROOT / ('.local/class-skills/' + name + '-lifecycle-acceptance')
        born = e.load(stage / 'birth-inputs.json')
        assert inputs['referenceVersion'] == receipt['referenceVersion'] == '3.3.93847'
        assert inputs['artifacts'] == receipt['artifacts'] == born['artifacts']
        assert markers(stage / 'birth-native-checks.log') == Counter(receipt['markers'])
        log = (stage / 'birth-native-checks.log').read_text()
        assert ' ERR]' not in log and ': failed' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        original_readers = []
        for path, digest in inputs['files'].items():
            if e.sha(ROOT / path) != digest:
                assert path == HELPER, (name, path)
                original_readers.append(dict(path=path, sha256=digest, commit=historical_helper(digest)))
        changes = [path for path, digest in born['sourceInputs'].items()
                   if path.startswith(('src/', 'shared/')) and path.endswith('.cs') and e.sha(ROOT / path) != digest]
        assert changes in ([], [sentinel]), (name, changes)
        if changes:
            assert born['sourceInputs'][sentinel] == original_sentinel
        else:
            assert receipt['artifacts']['Assemblies/Sanctuary.Content.dll'] == current['Assemblies/Sanctuary.Content.dll']
        for path, digest in receipt['artifacts'].items():
            if path not in ('Assemblies/Sanctuary.Content.dll', 'Assemblies/Sanctuary.Integration.dll'):
                assert current[path] == digest, (name, path)
        active = {r['talentId']: r for r in inputs['skills']}
        passive = {r['talentId']: r for r in inputs['passiveSkills']}
        expected = {key for key in rows if key.startswith(name + '.')}
        assert len(expected) == 30 and set(active).isdisjoint(passive) and set(active) | set(passive) == expected
        assert len(active) == inputs['activeTalents'] == inputs['verifiedActiveInputRoutes']
        assert len(passive) == inputs['passiveTalents']
        for key in sorted(expected):
            row = rows[key]
            assert row['implementation'] == 'handler-present' and row['verification'] == 'partial-native-coverage'
            if key in active:
                routes = active[key]['routes']
                assert routes and all(any(k.startswith('successful') and isinstance(v, int) and v > 0
                                          for k, v in route.items()) for route in routes.values()), key
            talents.append(dict(talentId=key, name=row['name'], characterClass=row['characterClass'],
                role='active' if key in active else 'passive', inputEvidence=active.get(key),
                passiveEvidence=passive.get(key), coverageMatrix='docs/' + name + '-coverage-matrix.md',
                remaining=row['remaining'], complete=False))
        classes.append(dict(characterClass=name, talents=30, active=len(active), passive=len(passive),
            exactRetainedNativeMarkers=sum(receipt['markers'].values()),
            receipt=str(receipt_path.relative_to(ROOT)), originalAuditReaders=original_readers,
            productionSourceChangesSinceCohort=changes, sentinelRepairBridge=bool(changes),
            currentContentArtifact=not changes, classCertified=False))
        files.extend([input_path, receipt_path, stage / 'birth-inputs.json', stage / 'birth-native-checks.log'])
    assert len(talents) == 240 and sum(c['active'] for c in classes) == 210 and sum(c['passive'] for c in classes) == 30
    approval = e.load(ROOT / 'tools/reference/find-item-chance-approval.json')
    assert approval['approved'] and not approval['gameplayChanged']
    assert e.sha(ROOT / approval['proposal']['path']) == approval['proposal']['sha256']
    for path, digest in approval['sourceHashes'].items():
        assert e.sha(ROOT / path) == digest
    next(row for row in talents if row['talentId'] == 'barbarian.finditem')['approvedPolicyOverrides'] = [
        dict(path='tools/reference/find-item-chance-approval.json', scope='chance only',
             overridesHistoricalPendingChanceText=True)]
    roster_path = ROOT / 'tools/reference/class-roster-ui-evidence.json'
    roster = e.load(roster_path)
    assert roster['renderedCreationVerified'] and roster['createdWireClass'] == 108
    assert roster['nativeChecks'] == 6 and roster['originalFilesUnchanged'] == 109
    assert not roster['installedGameplayChanged'] and not roster['combatVerified']
    for path, digest in roster['files'].items():
        assert e.sha(ROOT / path) == digest, path
    pets_path = ROOT / 'tools/reference/necromancer-pet-policy-audit.json'
    pets = e.load(pets_path)
    assert pets['reviewedRequirements'] == 22 and pets['currentSnapshotOriginalActors'] == 20
    assert pets['actualReloads'] == 2 and not pets['gameplayChanged'] and not pets['classCertified']
    for path, digest in pets['files'].items():
        assert e.sha(ROOT / path) == digest, path
    druid_path = ROOT / 'tools/reference/druid-pet-policy-audit.json'
    druid = e.load(druid_path)
    assert druid['reviewedRequirements'] == 11 and druid['preparedSpeciesParked'] == 10
    assert druid['safeAreaReloads'] == 2 and not druid['productionChanged'] and not druid['classCertified']
    for path, digest in druid['files'].items():
        assert e.sha(ROOT / path) == digest, path
    bash_path = ROOT / 'tools/reference/bash-flat-candidate-evidence.json'
    bash = e.load(bash_path)
    assert bash['status'] == 'tested-pending-user-decision' and not bash['approved']
    assert not bash['learnedRouteChanged'] and not bash['installedGameplayChanged']
    for path, digest in bash['files'].items():
        assert e.sha(ROOT / path) == digest, path
    intent_path = ROOT / 'tools/reference/class-mechanics-intent-decision.json'
    intent = e.load(intent_path)
    assert intent['approved'] and intent['bash']['approved'] and intent['findItem']['approved']
    assert intent['delegatedAdaptations']['approved'] and not intent['futureWorldBalance']['implemented']
    assert e.sha(ROOT / intent['bash']['proposal']) == intent['bash']['proposalSha256']
    rendered_path = ROOT / 'tools/reference/necromancer-rendered-evidence.json'
    rendered = e.load(rendered_path)
    assert rendered['realClient'] and rendered['learnedF1Raise'] and rendered['learnedF2TeethDamage']
    assert rendered['naturalOriginalWarriorCombat'] and not rendered['productionChanged']
    for path, digest in rendered['files'].items():
        assert e.sha(ROOT / path) == digest, path
    files.extend([install_path, original_path,
                  ROOT / 'tools/reference/class-skills-installation-evidence.json',
                  ROOT / 'data/Sanctuary/Balance/skill-effects.json',
                  ROOT / 'data/Sanctuary/Balance/skill-icons.json',
                  ROOT / 'tools/reference/find-item-chance-approval.json',
                  ROOT / approval['proposal']['path'], roster_path, pets_path, druid_path, bash_path, intent_path,
                  rendered_path,
                  ROOT / 'docs/class-mechanics-intent-decisions.md',
                  ROOT / 'docs/class-skill-completion-audit.md', Path(__file__)])
    result = dict(referenceVersion='3.3.93847', talents=240, classes=classes,
        activeRoutes=210, passiveConsumers=30, effectsRegisterEntries=240, iconRegisterEntries=240,
        installedArtifactsVerified=True, historicalEvidencePreserved=True,
        productionBridge='One existing Sentinel typed-component repair bridges the five earlier cohorts;its own focused/retained evidence remains separate.',
        findItemChanceApproved=True, runtimeOrNumericalDataChanged=False,
        renderedWarlockWitchCreationVerified=True,
        necromancerApprovedPetConsumersReviewed=True,
        druidApprovedPetConsumersReviewed=True,
        bashFlatRuleApprovedImplementationPending=True,
        findItemTargetingEligibilityApprovedImplementationPending=True,
        intentBasedMinorAdaptationAuthority=True,
        renderedNecromancerRepresentativeCombatVerified=True,
        strictCompletedClasses=ledger['completedClasses'], classCertified=False, goalComplete=False,
        remainingRequirements=['Resolve or explicitly qualify concrete ordinary-skill targeting/damage and special-recipient gaps',
            'Keep remaining shared summon contexts explicit;Necromancer and Druid selected pet changes have scoped consumer evidence',
            'Keep unsupported world services,legacy monster conversion and future gear design distinct',
            'Finish remaining representative rendered combat checks;Necromancer F1/F2/pet combat has scoped client evidence;final effects/audio and icons stay separate'],
        skills=talents)
    e.write('class-skill-completion-audit.json', result, files, check)
    print('Completion audit:240identities/210active routes/30passives/240effects+icons;old evidence and Sentinel bridge attributed;strict completion remains unproven.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    audit(parser.parse_args().check)
