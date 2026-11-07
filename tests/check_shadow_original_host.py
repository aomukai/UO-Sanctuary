#!/usr/bin/env python3
"""Shared original-NPC host: Shadow admission and retained Valkyrie execution."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_npc_original_contact import configuration as contact
from check_npc_item_skills import configuration as shadow

if __name__ == '__main__':
    files = contact(isolate=True)['extra_files']
    files.update(shadow(isolate=True)['extra_files'])
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/shadow-original-host', extra_files=files,
          extra_families=('shadow-original-events', 'shadow-body', 'npc-item-emission',
                          'npc-item-spell-flights', 'npc-item-chain', 'npc-original-contact'),
          extra_inputs=(Path(__file__).resolve(),))
