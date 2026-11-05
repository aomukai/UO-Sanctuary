#!/usr/bin/env python3
"""Record a source-only checkpoint without claiming runtime callback admission."""
import argparse
import json
import subprocess
from pathlib import Path

import skill_checkpoint_evidence as e

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/npc-lightning-return-reference'
PARENT = 'dc8a418538e59b2fc515b6d060ce8f162a397d54'


def build(check=False):
    source_path = ROOT / 'tools/reference/npc-lightning-return-source-evidence.json'
    source = e.load(source_path)
    assert source['contexts'] == dict(damage=864, absorb=144, guards=455)
    assert len(source['headerBindings']) == 140 and len(source['completeRoutineHashes']) == 7
    assert source['currentExecutableParity'] is False
    for group in ('inputs', 'artifacts'):
        for path, digest in source[group].items():
            assert e.sha(ROOT / path) == digest, path
    assert not (ROOT / '.local/class-skills/npc-lightning-return-source/sanitizer.log').read_bytes()
    # This checkpoint deliberately leaves production and its previously
    # accepted native assemblies untouched. Do not relabel a source check as
    # an actual incoming-weapon or save/reload acceptance test.
    subprocess.run(['git', 'diff', '--quiet', PARENT, '--', 'src', 'data', 'tests'], cwd=ROOT, check=True)
    parent = e.load(ROOT / 'tools/reference/npc-item-blind-native-evidence.json')
    assemblies = {}
    for kind in ('Content', 'Integration'):
        path = ROOT / '.local/class-skills/npc-item-blind' / kind.lower() / f'Sanctuary.{kind}.dll'
        digest = e.sha(path)
        assert digest == parent['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assemblies[str(path.relative_to(ROOT))] = digest
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses']
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 197
    commands = e.load(STAGE / 'verification-commands.json')
    expected = [
        ['python3', '-m', 'unittest', 'discover', '-s', 'tests', '-p', 'test_*.py'],
        ['python3', 'tools/build_named_readiness.py', '--check'],
        ['python3', 'tools/build_class_skills.py', '--check'],
        ['python3', 'tools/build_skill_roadmap.py', '--check'],
        ['python3', 'tools/probe_npc_lightning_return.py', '--check'],
    ]
    assert commands == [dict(argv=argv, exitCode=0) for argv in expected]
    tests = (STAGE / 'verification-1.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests
    protected = e.protection()
    files = [Path(__file__).resolve(), source_path, ROOT / 'tools/skill_checkpoint_evidence.py']
    files += [ROOT / path for group in ('inputs', 'artifacts') for path in source[group]]
    files += [ROOT / path for path in assemblies]
    files += [ROOT / 'src/Sanctuary.Content' / name for name in (
        'PreparedMonsterDamage.cs', 'PreparedMonsterNativeDamage.cs', 'InstantSpellNpcDamage.cs',
        'NativeMonsterReturn.cs', 'NpcEquipmentRetaliation.cs', 'NpcCombatEvents.cs',
        'NpcItemProcs.cs', 'NpcItemProcGraph.cs', 'PreparedWeaponEvents.cs',
        'ReviveEquipmentEvents.cs', 'IronGolemContact.cs', 'SkillCombat.cs',
        'EliteCombat.cs', 'NecromancerEffects.cs', 'PaladinThorns.cs')]
    native = ROOT / '.local/class-skills/player-melee-return/native/Projects/UOContent'
    files += [native / 'Misc/AOS.cs', native / 'Mobiles/ClassCombatHooks.cs']
    files += [ROOT / 'docs' / name for name in (
        'npc-lightning-return-reference-checkpoint.md', 'class-skills-resume-checkpoint.md',
        'class-skill-roadmap.md', 'skill-mechanical-reference.md', 'class-skill-reuse-notes.md',
        'skill-effects.md', 'skill-icons.md')]
    files += e.stage_files(STAGE)
    record = dict(referenceVersion='3.3.93847', parent=PARENT, installed=False, handlers=197,
                  completedClasses=[], productionUnchanged=True, nativeAcceptanceRun=False,
                  acceptedParentAssemblies=assemblies, sourceContexts=source['contexts'],
                  unchangedCompleteRoutines=7, headerConstants=140, pythonTests=71,
                  verificationCommands=5, protection=protected,
                  scope='Bounded older-source incoming-melee lightning return contract; full unchanged callback, mitigation, HP and NPC return compose with declared world/event/kill/recovery services. Current extracted/runtime event registration agrees. This is reference preparation, not production admission or modern executable equivalence.',
                  remaining=[
                      'Implement exactly-once incoming delivery across prepared legacy/graph and ordinary native AOS/PreparedWeaponEvents routes.',
                      'Preflight supported recipient mitigation/recovery/town/metadata services before mutation; retain explicit unsupported restrictions.',
                      'Verify learned original Coif Iron/frozen Revive, nested lethal/zero incoming loss/recursion/RNG and two reloads before admission.',
                      'Prior blind native2 contact assertion remains untraced; later identical-build passes are separate evidence.',
                      'No production build, save, configuration, client, handler/class-completion or asset claim is added.'])
    e.write('npc-lightning-return-reference-evidence.json', record, files, check)
    print('Lightning reference:1463 sanitized contexts/140 constants;71 Python/5 commands;parent build and protected installation unchanged;no native admission claimed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
