#!/usr/bin/env python3
"""Own defensive buff lists/casts plus affected Shadow and player consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_skills import configuration
if __name__=='__main__':
 files=configuration(isolate=True)['extra_files']
 files.update({'shadow-buff-animation-cases.json':ROOT/'.local/class-skills/shadow-buff-animation-source/cases.json',
               'fade-curse-source-probe.json':ROOT/'.local/class-skills/fade-curse-source-probe.json',
               'shadow-warrior-ai-cases.json':ROOT/'.local/class-skills/shadow-warrior-ai-source/cases.json',
               'shadow-ground-cases.json':ROOT/'.local/class-skills/shadow-ground-source/cases.json',
               'shadow-animation-cases.json':ROOT/'.local/class-skills/shadow-animation-source/cases.json'})
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/shadow-buffs',extra_files=files,
       extra_families=('shadow-buffs','shadow-ground','shadow-actions','fade'),
       extra_wait_results={'fade-check':'fade-result','fade-reload-check':'fade-reload-result'},
       extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_buff_animation.py',ROOT/'tools/reference/assassin-buff-engine-evidence.json'))
