#!/usr/bin/env python3
"""Own NPC Flight action,ground transfer and retained player/kick consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-flight'
 c['extra_files'].update({'shadow-talon-cases.json':ROOT/'.local/class-skills/shadow-talon-source/cases.json','shadow-kick-rewind-cases.json':ROOT/'.local/class-skills/shadow-kick-rewind-source/cases.json','shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_families']=('shadow-flight','shadow-kick-packets','shadow-tail','shadow-talon','dragon-flight-landing','dragon-flight')
 c['extra_checks']=('dragon-flight-mechanics-check','leap-geometry-check')
 c['extra_files'].update({'free-coordinates-cases.json':ROOT/'.local/class-skills/free-coordinates-source/cases.json','leap-source-probe.json':ROOT/'.local/class-skills/leap-source-probe.json','leap-collision-source-probe.json':ROOT/'.local/class-skills/leap-collision-source-probe.json'})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-flight-source-review.json',ROOT/'tools/reference/dragon-flight-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
