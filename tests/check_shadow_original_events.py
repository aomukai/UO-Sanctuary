#!/usr/bin/env python3
"""Focused shared-registry compatibility for Shadows and original Valkyries."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_original_events import configuration as previous
from check_npc_item_skills import configuration as shadow_inputs

if __name__=='__main__':
    old=previous(isolate=True)
    stage=ROOT/'.local/class-skills/shadow-original-events'
    files={k:v for k,v in old['extra_files'].items() if k in ['npc-original-events-cases.json','valkyrie-damage-cases.json']}
    files.update(shadow_inputs(isolate=True)['extra_files'])
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=stage,extra_files=files,extra_families=('shadow-original-events','shadow-body','npc-original-events'),extra_inputs=(Path(__file__).resolve(),))
