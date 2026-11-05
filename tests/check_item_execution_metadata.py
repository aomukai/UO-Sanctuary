#!/usr/bin/env python3
"""All current execution metadata and actual eight-curse NPC service checks."""
import argparse,os
from pathlib import Path
from check_skill_execution import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('item-execution-metadata-broader' if broader else 'item-execution-metadata')
    ref=ROOT/'tools/reference/d2r-named-equipment/json'
    c['extra_files'].update({'item-execution-raw-skills.json':ref/'skills.json','item-execution-raw-missiles.json':ref/'missiles.json'})
    c['extra_families']=('item-execution-metadata',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/item-skill-execution-metadata.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
