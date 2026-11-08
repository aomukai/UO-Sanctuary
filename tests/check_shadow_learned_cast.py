#!/usr/bin/env python3
"""Learned Shadow transactions,right/quick real combat and retained intrinsic Fury."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-learned-cast'
 c['extra_families']=('shadow-cast','shadow-fury-cast','shadow-master-world')
 c['extra_target_input_commands']=('shadow-cast-right','shadow-cast-quick')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-fury-cast-native-evidence.json',ROOT/'tools/reference/shadow-master-range-native-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
