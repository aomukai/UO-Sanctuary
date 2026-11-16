#!/usr/bin/env python3
"""Whole original cold-duration transport, learned casts and legacy refusal."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/cold-parameter'
INPUTS = ('cold-parameter-right', 'cold-parameter-quick')


def configuration():
    return dict(stage=STAGE,
        extra_families=('iron-golem-cast', 'cold-parameter', 'npc-equipment-elements', 'item-state'),
        extra_manifest_names={'item-state': 'item-state-checks.json'},
        extra_target_input_commands=INPUTS,
        extra_files={'npc-equipment-elements-source-cases.json': ROOT / '.local/class-skills/npc-equipment-elements-source/cases.json'},
        extra_inputs=(Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py',
            ROOT / 'tools/reference/item-property-writers-source-evidence.json'))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
