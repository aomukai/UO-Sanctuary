#!/usr/bin/env python3
"""Original Shadow defense with retained original-NPC host execution."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_npc_original_contact import configuration as contact
from check_npc_item_skills import configuration as shadow

if __name__ == '__main__':
    files = contact(isolate=True)['extra_files']
    files.update(shadow(isolate=True)['extra_files'])
    files['source-avoidance-cases.json'] = ROOT/'.local/class-skills/source-avoidance-source/cases.json'
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/shadow-defense', extra_files=files,
          extra_families=('shadow-defense', 'shadow-original-events', 'shadow-body', 'npc-item-emission',
                          'npc-item-spell-flights', 'npc-item-chain', 'npc-original-contact', 'valkyrie-defense'),
          extra_inputs=(Path(__file__).resolve(),))
