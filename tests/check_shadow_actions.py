#!/usr/bin/env python3
"""Focused Shadow event conductor, retained intrinsic/defense/player casts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_skills import configuration as shadow
if __name__=='__main__':
    files=shadow(isolate=True)['extra_files']
    files.update({'shadow-animation-cases.json':ROOT/'.local/class-skills/shadow-animation-source/cases.json',
                  'fire-blast-timing-source-probe.json':ROOT/'.local/class-skills/fire-blast-timing-source-probe.json'})
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/shadow-actions',extra_files=files,
          extra_families=('shadow-actions','shadow-fire-blast','shadow-defense','fire-blast'),
          extra_wait_results={'fire-blast-check':'fire-blast-result','fire-blast-reload-check':'fire-blast-reload-result'},
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_animation.py',ROOT/'tools/reference/shadow-animation-source-evidence.json'))
