#!/usr/bin/env python3
"""Own NPC Dragon Claw and affected claw/finisher/elemental consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-dragon-claw'
 c['extra_files'].update({'shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json','shadow-claw-cases.json':ROOT/'.local/class-skills/shadow-claw-sequence-source/cases.json','shadow-claw-rates.json':ROOT/'.local/class-skills/shadow-claw-sequence-source/rates.json','shadow-charge-animation-cases.json':ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_families']=('shadow-dragon-claw','shadow-claw','shadow-finisher','shadow-ice','shadow-thunder')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_dragon_claw.py',ROOT/'tools/reference/shadow-dragon-claw-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
