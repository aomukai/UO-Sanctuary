#!/usr/bin/env python3
"""Own NPC kick capture and retained original Dragon Claw contacts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-kick'
 c['extra_files'].update({'shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json'})
 c['extra_families']=('shadow-kick','shadow-dragon-claw')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/dragon-kick-damage-source-evidence.json',ROOT/'tools/reference/dragon-kick-packet-source-evidence.json',ROOT/'tools/reference/dragon-talon-hands-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
