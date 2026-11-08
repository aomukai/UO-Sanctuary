#!/usr/bin/env python3
"""Original dead Shadow nova plus existing original-NPC spell consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-death'
 c['extra_families']=('shadow-death','npc-item-spell-flights','shadow-unique')
 c['extra_checks']=('shadow-death-live',)
 c['extra_connected_checks']=('shadow-death-live',)
 c['extra_wait_results']={'shadow-death-live':'shadow-death-live-result'}
 c['extra_files']['shadow-death-source-cases.json']=ROOT/'.local/class-skills/shadow-death-source/cases.json'
 c['extra_inputs']+=(ROOT/'tools/probe_shadow_death.py',ROOT/'tools/reference/shadow-death-source-evidence.json')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-death-modifier-source-review.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
