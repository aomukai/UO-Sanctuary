#!/usr/bin/env python3
"""Warrior AI/movement with retained original Shadow action/contact consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_skills import configuration
if __name__=='__main__':
 files=configuration(isolate=True)['extra_files']
 files.update({'shadow-warrior-ai-cases.json':ROOT/'.local/class-skills/shadow-warrior-ai-source/cases.json',
               'shadow-ground-cases.json':ROOT/'.local/class-skills/shadow-ground-source/cases.json',
               'shadow-melee-cases.json':ROOT/'.local/class-skills/shadow-animation-source/melee-cases.json',
               'shadow-animation-cases.json':ROOT/'.local/class-skills/shadow-animation-source/cases.json'})
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/shadow-ground',extra_files=files,
       extra_families=('shadow-ground','shadow-melee','shadow-actions'),
       extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_warrior_ai.py',ROOT/'tools/probe_shadow_ground_motion.py'))
