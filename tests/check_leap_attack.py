#!/usr/bin/env python3
"""Learned Leap Attack,ordinary Leap/War Cry and retained class controls."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_leap_attack_sequence import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else prior(False)
 c['stage']=ROOT/'.local/class-skills'/('leap-attack-retained'if retained else'leap-attack')
 c['extra_families']+=('leap','leap-attack','warcry')
 c.setdefault('extra_wait_results',{}).update({'leap-check':'leap-result','leap-reload-check':'leap-result','warcry-check':'warcry-result','warcry-reload-check':'warcry-result'})
 c.setdefault('extra_manifest_names',{})['warcry']='warcry.json'
 c.setdefault('extra_target_input_commands',());c['extra_target_input_commands']+=('leap-attack-left','leap-attack-right','leap-attack-quick')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
