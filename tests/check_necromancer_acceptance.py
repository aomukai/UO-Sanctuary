#!/usr/bin/env python3
"""One saved cohort for Necromancer spells, summons and the shared AI consumers.

This runs existing acceptance scopes together. A passing cohort alone does not
close explicitly uncertain source/host adapters or certify the entire class.
"""
import json
import sys
from pathlib import Path

from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT, check


def combined():
    # Short-lived barrier records must be inspected early on each reload.
    scripts = ('check_bone_barrier_clock.py', 'check_confuse_resume.py',
               'check_blind_world.py', 'check_attract_cast.py', 'check_summon_resist.py')
    configs = [configuration(name) for name in scripts]
    result = {'stage': ROOT / '.local/class-skills/necromancer-acceptance'}
    for key in ('extra_families', 'extra_checks', 'extra_live_commands', 'extra_inputs',
                'extra_target_input_commands', 'extra_connected_checks', 'extra_one_reload_families',
                'extra_ground_input_commands'):
        result[key] = tuple(dict.fromkeys(item for config in configs for item in config.get(key, ())))
    for key in ('extra_files', 'extra_wait_results', 'extra_manifest_names', 'extra_input_timeouts'):
        merged = {}
        for config in configs:
            for name, value in config.get(key, {}).items():
                assert name not in merged or merged[name] == value, (key, name)
                merged[name] = value
        result[key] = merged
    result['extra_families'] += ('corpse-cast', 'revive-cast')
    result['extra_files'].update({
        'necro-corpse-explosion-source-probe.json': ROOT / '.local/class-skills/necro-corpse-explosion-source-probe.json',
        'revive-source-policy-source-probe.json': ROOT / '.local/class-skills/revive-source-policy-source-probe.json',
    })
    result['extra_manifest_names'].update({'corpse-cast': 'corpse-explosion-cast-fixtures.json'})
    result['extra_inputs'] += (Path(__file__).resolve(), *(ROOT / 'tests' / name for name in scripts))
    result['save_command'] = result['save_result'] = 'necromancer-acceptance-save'
    result['extra_early_reload_families'] = ('bone-barrier-clock', 'bone-wall-maker', 'bone-barrier-birth', 'bone-barriers')
    for path in result['extra_files'].values():
        assert path.is_file(), path
    return result


if __name__ == '__main__':
    config = combined()
    if '--save-smoke' in sys.argv:
        config.update(stage=ROOT / '.local/class-skills/necromancer-acceptance-targeted',
            extra_families=('bone-barrier-clock', 'bone-wall-maker', 'bone-barrier-cast', 'bone-barrier-birth',
                            'bone-barriers', 'fire-golem-death', 'summon-resist', 'revive-cast', 'corpse-cast'),
            extra_checks=(), extra_live_commands=('fire-golem-death-live',), extra_connected_checks=(),
            extra_one_reload_families=(), extra_target_input_commands=(), extra_ground_input_commands=())
    if '--plan' in sys.argv:
        print(json.dumps({key: len(config[key]) for key in (
            'extra_families', 'extra_checks', 'extra_live_commands', 'extra_target_input_commands',
            'extra_files', 'extra_connected_checks')}, indent=2))
        print('No conflicting probe/wait/manifest/timeout mappings;all source probes exist;one combined special-state save.')
    else:
        check(**config)
