#!/usr/bin/env python3
"""Death Sentry corpse contacts retain typed Health Link components."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/sentry-explosion-link'
def configuration():
    names=('defiler-health-link-check','defiler-health-link-reload-check','death-learned-check','death-learned-reload-check')
    c=dict(stage=STAGE,extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=('defiler-health-link','hellslayer-readiness','death-explosion','death-control','death-learned'),extra_checks=('death-learned-mechanics-check',),extra_manifest_names={'death-explosion':'death-sentry-explosion-fixtures.json'},extra_wait_results={names[0]:names[0]+'-result',names[1]:names[1]+'-result',names[2]:'death-learned-result',names[3]:'death-learned-reload-result'},extra_connected_checks=names,extra_connection_settle=.15,extra_target_input_commands=('death-learned-left','death-learned-right','death-learned-quick'),extra_ground_input_commands=('death-learned-left','death-learned-right','death-learned-quick'),extra_instant_input_commands=('death-learned-left',),extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/mage-spell-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py'))
    for n in ('death-sentry-ai','death-sentry-corpse-search','corpse-explosion','death-sentry-clock'):c['extra_files'][n+'-source-probe.json']=ROOT/'.local/class-skills'/(n+'-source-probe.json')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
