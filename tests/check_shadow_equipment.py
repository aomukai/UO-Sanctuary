#!/usr/bin/env python3
"""Prepared Shadow equipment requests with retained Valkyrie and learned cohorts."""
import argparse,os
from pathlib import Path
from check_dragon_flight_landing import ROOT,configuration as previous
from check_iron_golem_birth import check
def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('shadow-equipment-broader' if broader else 'shadow-equipment')
    c['extra_files'].update({'shadow-equipment-cases.json':ROOT/'.local/class-skills/shadow-equipment-source/cases.json',
        'valkyrie-affix-source-probe.json':ROOT/'.local/class-skills/valkyrie-affix-source-probe.json',
        'valkyrie-equipment-source-probe.json':ROOT/'.local/class-skills/valkyrie-equipment-source-probe.json'})
    c['extra_families']+=('shadow-equipment','valkyrie-affix')
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-equipment-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
