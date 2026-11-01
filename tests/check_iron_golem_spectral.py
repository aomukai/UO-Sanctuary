#!/usr/bin/env python3
"""Iron shared Spectral Hit and retained Strong/curve/player/pet scopes."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT, check
if __name__ == '__main__':
    base = configuration('check_iron_golem_strong.py')
    base['stage'] = ROOT / '.local/class-skills/iron-golem-spectral'
    base['extra_families'] = ('iron-golem-spectral', *base['extra_families'])
    base['extra_live_commands'] = ('iron-golem-spectral-live', *base['extra_live_commands'])
    base['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tests/check_iron_golem_strong.py')
    check(**base)
