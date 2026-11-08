#!/usr/bin/env python3
"""Own NPC Fury stat snapshot and original actor persistence."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-fury-packets'
 c['extra_files'].update({'shadow-talon-cases.json':ROOT/'.local/class-skills/shadow-talon-source/cases.json','shadow-kick-rewind-cases.json':ROOT/'.local/class-skills/shadow-kick-rewind-source/cases.json','shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_files']['shadow-fury-packet-cases.json']=ROOT/'.local/class-skills/shadow-fury-packets-source/cases.json'
 c['extra_families']=('shadow-fury-packets','item-execution-metadata','npc-item-skills','blade-fury-missile')
 c['extra_manifest_names']={'blade-fury-missile':'blade-fury-missile-fixture.json'}
 c['extra_files'].update({'item-execution-raw-skills.json':ROOT/'tools/reference/d2r-named-equipment/json/skills.json','item-execution-raw-missiles.json':ROOT/'tools/reference/d2r-named-equipment/json/missiles.json','blade-fury-damage-source-probe.json':ROOT/'.local/class-skills/blade-fury-damage-source-probe.json'})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-fury-packets-source-evidence.json',ROOT/'docs/shadow-fury-mana-proposal.md')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
