#!/usr/bin/env python3
"""Hammer curved path14,source RNG,lifecycle and natural original-flight reloads."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_blessed_hammer_damage import configuration as damage

def configuration(full=False):
 c=damage(full);c['stage']=ROOT/'.local/class-skills/blessed-hammer-flight'
 c['extra_families']+=('blessed-hammer-flight',)
 c['extra_manifest_names']['blessed-hammer-flight']='blessed-hammer-flight-checks.json'
 c['extra_wait_results'].update({'blessed-hammer-flight-check':'blessed-hammer-flight-result','blessed-hammer-flight-reload-check':'blessed-hammer-flight-reload-result'})
 c['extra_connected_checks']=c.get('extra_connected_checks',())+('blessed-hammer-flight-reload-check',)
 c['extra_checks']+=('actor-motion-check',)
 c['extra_files'].update({'hammer-motion-source-cases.json':ROOT/'.local/class-skills/blessed-hammer-flight/motion-source/cases.json','actor-motion-source-probe.json':ROOT/'.local/class-skills/actor-motion-source-probe.json'})
 c['extra_target_input_commands']+=tuple('blessed-hammer-flight-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_blessed_hammer_motion.py',ROOT/'tools/reference/blessed-hammer-motion-engine-evidence.json',ROOT/'tools/reference/actor-motion-engine-evidence.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
