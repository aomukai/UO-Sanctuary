#!/usr/bin/env python3
"""Original Shadow melee and retained equipped-NPC/S2 consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_original_contact import configuration as contact
from check_npc_item_skills import configuration as shadow
if __name__=='__main__':
    files=contact(isolate=True)['extra_files'];files.update(shadow(isolate=True)['extra_files'])
    files.update({'shadow-melee-cases.json':ROOT/'.local/class-skills/shadow-animation-source/melee-cases.json',
                  'shadow-animation-cases.json':ROOT/'.local/class-skills/shadow-animation-source/cases.json'})
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/shadow-melee',extra_files=files,
          extra_families=('shadow-melee','shadow-actions','shadow-fire-blast','npc-original-contact'),
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_animation.py',ROOT/'tools/reference/shadow-animation-source-evidence.json'))
