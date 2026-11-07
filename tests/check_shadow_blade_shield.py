#!/usr/bin/env python3
"""Original NPC Blade Shield,shared player scheduler and original contact consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
from check_npc_original_contact import configuration as contact
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-blade-shield'
 c['extra_files'].update(contact(isolate=True)['extra_files'])
 c['extra_files'].update({'npc-blade-shield-fill-cases.json':ROOT/'.local/class-skills/npc-damage-fill-shield-source/cases.json',
                         'blade-shield-damage-source-probe.json':ROOT/'.local/class-skills/blade-shield-damage-source-probe.json'})
 c['extra_families']=('shadow-blade-shield','shadow-actions','shadow-item-frame','npc-original-contact','blade-shield')
 c['extra_wait_results']={'blade-shield-check':'blade-shield-result','blade-shield-reload-check':'blade-shield-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/npc-blade-shield-fill-source-evidence.json',ROOT/'tools/reference/blade-shield-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
