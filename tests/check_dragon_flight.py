#!/usr/bin/env python3
"""Approved Flight action retains dragon finishers and real Teleport/Leap controls."""
import argparse
import os
from pathlib import Path
from check_dragon_claw import ROOT, configuration as previous
from check_dragon_flight_landing import configuration as landing
from check_dragon_flight_mode import configuration as mode
from check_iron_golem_birth import check


def configuration(broader=False,isolate=False):
    c=dict(extra_files={},extra_families=(),extra_inputs=()) if isolate else previous(broader)
    suffix='-isolate' if isolate else '-broader' if broader else ''
    c['stage']=ROOT/('.local/class-skills/dragon-flight-action'+suffix)
    if not isolate:
        old=mode(broader);extra=landing(broader)
        c['extra_files'].update(extra['extra_files'])
        for key in ['extra_families','extra_checks','extra_connected_checks','extra_target_input_commands','extra_ground_input_commands']:
            c[key]=(*c.get(key,()),*(x for x in extra.get(key,()) if x not in old.get(key,())))
        c.setdefault('extra_wait_results',{}).update(extra['extra_wait_results'])
        c['save_command']=extra['save_command'];c['save_result']=extra['save_result']
    c['extra_families']+=('dragon-flight',)
    c['extra_checks']=(*c.get('extra_checks',()),'dragon-flight-mechanics-check')
    c['extra_target_input_commands']=(*c.get('extra_target_input_commands',()),'dragon-flight-left','dragon-flight-right','dragon-flight-quick')
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'docs/dragon-flight-approved-action-rule.md',ROOT/'tools/reference/dragon-flight-source-evidence.json',ROOT/'tools/reference/free-coordinates-source-evidence.json')
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--isolate',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.isolate))
