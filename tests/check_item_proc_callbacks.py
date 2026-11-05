#!/usr/bin/env python3
"""Shared proc callback contracts with actual Blood Life Tap and retained cohorts."""
import argparse,os
from pathlib import Path
from check_item_skill_properties import ROOT,configuration as previous
from check_iron_golem_birth import check
def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('item-proc-callbacks-broader' if broader else 'item-proc-callbacks')
    c['extra_files'].update({'item-proc-callback-cases.json':ROOT/'.local/class-skills/item-proc-callbacks-source/cases.json',
        'npc-hit-skill-source-cases.json':ROOT/'.local/class-skills/npc-hit-skill-source/cases.json'})
    c['extra_families']+=('item-proc-callback','blood-life-tap')
    c['extra_live_commands']=c.get('extra_live_commands',())+('blood-life-tap-live',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/item-proc-callbacks-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
