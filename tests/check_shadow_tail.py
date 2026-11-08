#!/usr/bin/env python3
"""Own NPC Tail with affected shared kick/typed/charge consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-tail'
 c['extra_files'].update({'shadow-talon-cases.json':ROOT/'.local/class-skills/shadow-talon-source/cases.json','shadow-kick-rewind-cases.json':ROOT/'.local/class-skills/shadow-kick-rewind-source/cases.json','shadow-dragon-claw-cases.json':ROOT/'.local/class-skills/shadow-dragon-claw-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_families']=('shadow-tail','shadow-talon','shadow-kick-packets','shadow-ice','shadow-thunder')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/dragon-tail-source-evidence.json',ROOT/'tools/reference/dragon-tail-burst-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
