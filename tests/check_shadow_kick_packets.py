#!/usr/bin/env python3
"""Own NPC kick packet and affected original contact/finisher consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-kick-packets'
 c['extra_files'].update({'shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json','valkyrie-damage-cases.json':ROOT/'.local/class-skills/valkyrie-damage-source/cases.json'})
 c['extra_families']=('shadow-kick-packets','shadow-kick','shadow-dragon-claw','shadow-finisher','npc-original-contact')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/npc-damage-fill-source-evidence.json',ROOT/'tools/reference/shadow-finisher-source-review.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
