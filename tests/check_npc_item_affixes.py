#!/usr/bin/env python3
"""Complete current NPC pools/names with retained magic/rare and summon cohorts."""
import argparse,os
from pathlib import Path
from check_item_affixes import ROOT,configuration as previous
from check_iron_golem_birth import check
def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('npc-item-affixes-broader' if broader else 'npc-item-affixes')
    c['extra_files'].update({'npc-item-affix-source-probe.json':ROOT/'.local/class-skills/npc-item-affix-source-probe.json','npc-rare-name-cases.json':ROOT/'.local/class-skills/npc-rare-names-source/cases.json'})
    c['extra_families']+=('npc-item-affix',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/npc-item-affix-engine-evidence.json',ROOT/'tools/reference/npc-rare-names-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
