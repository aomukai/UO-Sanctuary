#!/usr/bin/env python3
"""Learned Ward corpse/child/UTC/context/owner/save and retained class mechanics."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_frenzy import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_files={},extra_checks=(),extra_connected_checks=(),extra_wait_results={},extra_target_input_commands=())
 c['stage']=ROOT/'.local/class-skills'/('grim-ward-actor-retained'if retained else'grim-ward-actor')
 c['extra_families']+=('grim-ward-actor',)
 c['extra_files'].update({f'grim-ward-{n}-source-probe.json':ROOT/'.local/class-skills'/f'grim-ward-{n}-source-probe.json'for n in ['curse','lifecycle']})
 c['extra_files']['barbarian-cast-clock-source-probe.json']=ROOT/'.local/class-skills/barbarian-cast-clock-source-probe.json'
 c['extra_checks']+=('grim-ward-world-pulse-check','grim-ward-curse-check','grim-ward-lifecycle-check')
 if not retained:c['extra_checks']+=('barbarian-cast-clock-check',)
 commands=('grim-ward-actor-check','grim-ward-actor-reload-check')
 c['extra_connected_checks']+=commands
 c['extra_wait_results'].update({x:x+'-result'for x in commands})
 c['extra_target_input_commands']+=('grim-ward-right','grim-ward-quick')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
