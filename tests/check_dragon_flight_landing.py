#!/usr/bin/env python3
"""Prepared Flight landing with original Leap and actual learned Teleport inputs."""
import argparse
import os
from pathlib import Path
from check_dragon_flight_mode import ROOT,configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('dragon-flight-landing-broader' if broader else 'dragon-flight-landing')
    c['extra_files'].update({'free-coordinates-cases.json':ROOT/'.local/class-skills/free-coordinates-source/cases.json',
        'leap-source-probe.json':ROOT/'.local/class-skills/leap-source-probe.json',
        'leap-collision-source-probe.json':ROOT/'.local/class-skills/leap-collision-source-probe.json'})
    c['extra_families']+=('dragon-flight-landing','player-teleport')
    c['extra_checks']=(*c.get('extra_checks',()),'leap-geometry-check')
    c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'player-teleport-reload-check')
    c.setdefault('extra_wait_results',{})['player-teleport-reload-check']='player-teleport-reload-result'
    names=('player-teleport-left','player-teleport-right','player-teleport-quick')
    c['extra_target_input_commands']=(*c.get('extra_target_input_commands',()),*names)
    c['extra_ground_input_commands']=(*c.get('extra_ground_input_commands',()),*names)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/free-coordinates-source-evidence.json')
    c['save_command']='player-teleport-save';c['save_result']='player-teleport-save'
    return c


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(args.broader))
