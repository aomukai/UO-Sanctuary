#!/usr/bin/env python3
"""Original item requests bound to actual saved Fire Bolt and partial rings."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_npc_item_spell_origins import configuration as prior


def configuration(retained=False, isolate=False):
    if isolate:
        return dict(stage=ROOT/'.local/class-skills/npc-item-emission-isolate',
            extra_families=('npc-item-emission',),
            extra_files={'valkyrie-damage-cases.json':ROOT/'.local/class-skills/valkyrie-damage-source/cases.json'},
            extra_inputs=(Path(__file__).resolve(),))
    c = prior(retained)
    c['stage'] = ROOT / '.local/class-skills' / ('npc-item-emission-retained' if retained else 'npc-item-emission')
    c['extra_families'] += ('npc-item-emission',)
    c['extra_inputs'] += (Path(__file__).resolve(),)
    return c


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--retained', action='store_true')
    parser.add_argument('--isolate', action='store_true')
    args = parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.retained, args.isolate))
