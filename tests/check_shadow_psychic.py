#!/usr/bin/env python3
"""Intrinsic NPC Hammer,split direct-spell receiver and retained consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-psychic'
 c['extra_families']=('shadow-psychic','shadow-venom','shadow-ground');c['extra_wait_results']={}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/psychic-hammer-engine-evidence.json',ROOT/'tools/reference/psychic-hammer-damage-engine-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
