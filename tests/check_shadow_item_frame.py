#!/usr/bin/env python3
"""Original selected-hand reconstruction regression and affected consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
from check_npc_original_contact import configuration as original_contact
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-item-frame'
 c['extra_files'].update(original_contact(isolate=True)['extra_files'])
 c['extra_families']=('shadow-item-frame','shadow-venom','shadow-ground','npc-original-contact');c['extra_wait_results']={}
 c['extra_inputs']+=(Path(__file__).resolve(),)
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
