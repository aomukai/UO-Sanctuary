#!/usr/bin/env python3
"""Iron live direct Extra Strong and retained curve/item/player/pet scopes."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT, check
if __name__ == '__main__':
    base = configuration('check_iron_golem_damage.py')
    base['stage'] = ROOT / '.local/class-skills/iron-golem-strong'
    base['extra_families'] = ('iron-golem-strong', *base['extra_families'])
    base['extra_live_commands'] = ('iron-golem-strong-live', *base['extra_live_commands'])
    base['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tests/check_iron_golem_damage.py')
    check(**base)
