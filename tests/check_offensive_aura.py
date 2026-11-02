#!/usr/bin/env python3
"""Offensive aura real rank packets,multi-stat callbacks and existing timing/recovery."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_resource_aura import configuration as parent
def configuration(full=False):
    c=parent(full);c['stage']=ROOT/'.local/class-skills/offensive-aura'
    c['extra_checks']+=('offensive-aura-check',)
    names=tuple('offensive-aura-'+ability+'-'+mode for ability in ('concentration','fanaticism') for mode in ('right','quick'))
    c['extra_target_input_commands']+=names;c['extra_instant_input_commands']+=names
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_offensive_aura_callback.py',ROOT/'tools/reference/offensive-aura-callback-source-evidence.json',ROOT/'.local/class-skills/offensive-aura-callback-source/cases.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
