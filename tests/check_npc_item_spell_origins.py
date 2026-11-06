#!/usr/bin/env python3
"""Retain original NPC prepared spell consumers alongside item origin traces."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_npc_item_chain import configuration as prior


def configuration(retained=False):
    c = prior(retained)
    c['stage'] = ROOT / '.local/class-skills' / ('npc-item-spell-origins-retained' if retained else 'npc-item-spell-origins')
    # This source-contract check owns no world entities or saved fixture.
    # Existing original pet/flight families still exercise both real reloads.
    c['extra_checks'] += ('npc-item-spell-origins-check',)
    c['extra_files']['npc-item-spell-origins-cases.json'] = ROOT / '.local/class-skills/npc-item-spell-origins-source/cases.json'
    c['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/npc-item-spell-origins-source-evidence.json')
    return c


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--retained', action='store_true')
    args = parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.retained))
