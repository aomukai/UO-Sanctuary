#!/usr/bin/env python3
"""Blood fixed NPC Life Tap, all affected curse casts and retained wound scopes."""
from pathlib import Path
import runpy
import check_iron_golem_birth as native
from check_iron_golem_birth import ROOT, check


def configuration(filename):
    """Reuse existing regression declarations without executing their server."""
    captured = []
    original = native.check
    try:
        native.check = lambda **kwargs: captured.append(kwargs)
        runpy.run_path(str(ROOT / 'tests' / filename), run_name='__main__')
    finally:
        native.check = original
    assert len(captured) == 1
    return captured[0]


if __name__ == '__main__':
    base = configuration('check_npc_open_wounds.py')
    curses = configuration('check_life_tap_cast.py')
    ai = configuration('check_confuse_cast.py')
    base['stage'] = ROOT / '.local/class-skills/blood-life-tap'
    base['extra_files'].update(curses['extra_files'])
    base['extra_files'].update(ai['extra_files'])
    base['extra_files']['npc-hit-skill-source-cases.json'] = ROOT / '.local/class-skills/npc-hit-skill-source/cases.json'
    affected = ('blood-life-tap', 'life-tap-cast', 'iron-maiden', 'lower-resist', 'decrepify', 'weaken',
                'amplify-damage', 'dim-vision-cast', 'terror-cast', 'confuse-cast')
    base['extra_families'] = tuple(dict.fromkeys((*affected, *base['extra_families'])))
    base['extra_live_commands'] = ('blood-life-tap-live', *base['extra_live_commands'])
    base['extra_target_input_commands'] += ('life-tap-cast-right', 'life-tap-cast-quick')
    base['extra_input_timeouts'] = {'life-tap-cast-right': 25, 'life-tap-cast-quick': 25}
    base['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tests/check_life_tap_cast.py', ROOT / 'tests/check_confuse_cast.py')
    check(**base)
