#!/usr/bin/env python3
"""Current SQ11, legacy do70 and retained real selected-hand items."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_necromancer_item_skills import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={},extra_checks=(),extra_manifest_names={})
 c['stage']=ROOT/'.local/class-skills'/('double-swing-sequence-retained'if retained else'double-swing-sequence')
 c['extra_families']+=('double-swing-sequence','weapon-hands','double-swing','barbarian-binding')
 c['extra_checks']+=('double-swing-phase-check','barbarian-cast-clock-check')
 c['extra_manifest_names']['weapon-hands']='weapon-hands.json'
 c['extra_files']['double-swing-sequence-cases.json']=ROOT/'.local/class-skills/double-swing-sequence-source/cases.json'
 c['extra_files']['double-swing-phase-source-probe.json']=ROOT/'.local/class-skills/double-swing-phase-source-probe.json'
 c.setdefault('extra_target_input_commands',());c['extra_target_input_commands']+=('double-swing-left','double-swing-right','double-swing-quick')
 c['extra_files']['barbarian-binding-reference.json']=ROOT/'tools/reference/barbarian-binding-reference.json'
 c['extra_files']['barbarian-cast-clock-source-probe.json']=ROOT/'.local/class-skills/barbarian-cast-clock-source-probe.json'
 c['extra_files']['battle-orders-source-probe.json']=ROOT/'.local/class-skills/battle-orders-source-probe.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
