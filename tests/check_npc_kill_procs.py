#!/usr/bin/env python3
"""NPC actual on-kill procs with retained event/cast/class cohorts."""
import argparse,os
from pathlib import Path
from check_unit_events import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
 c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('npc-kill-procs-broader' if broader else 'npc-kill-procs')
 c['extra_families']=('npc-kill-proc',)+c['extra_families'];c['extra_live_commands']=('npc-kill-proc-live',)+c.get('extra_live_commands',())
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/npc-kill-proc-source-review.json');return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
