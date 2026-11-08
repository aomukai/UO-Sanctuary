#!/usr/bin/env python3
"""Original Fury sequence/critical proposal plus retained missile and Flight consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-fury-action'
 c['extra_families']=('shadow-fury-clock','shadow-fury-missiles','blade-fury-missile','shadow-flight')
 c['extra_manifest_names']={'blade-fury-missile':'blade-fury-missile-fixture.json'}
 c['extra_files'].update({'shadow-fury-clock-cases.json':ROOT/'.local/class-skills/shadow-fury-clock-source/cases.json','shadow-fury-action-cases.json':ROOT/'.local/class-skills/shadow-fury-action-source/cases.json','shadow-fury-missile-cases.json':ROOT/'.local/class-skills/shadow-fury-missiles-source/cases.json','blade-fury-damage-source-probe.json':ROOT/'.local/class-skills/blade-fury-damage-source-probe.json','shadow-talon-cases.json':ROOT/'.local/class-skills/shadow-talon-source/cases.json','shadow-kick-rewind-cases.json':ROOT/'.local/class-skills/shadow-kick-rewind-source/cases.json','shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_fury_action.py',ROOT/'tools/reference/shadow-fury-action-source-evidence.json',ROOT/'tools/reference/shadow-fury-clock-source-evidence.json',ROOT/'tools/probe_shadow_fury_clock.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
