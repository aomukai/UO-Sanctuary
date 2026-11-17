#!/usr/bin/env python3
"""Known Druid spell components and retained full Druid acceptance scope."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_planned_acceptance import configuration as prior
STAGE=ROOT/'.local/class-skills/druid-element-link'
def configuration():
    c=prior();c['stage']=STAGE
    c['extra_families']=('defiler-health-link','hellslayer-readiness',*c['extra_families'])
    c['extra_files']['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    names=('defiler-health-link-check','defiler-health-link-reload-check')
    c['extra_connected_checks']+=names;c['extra_wait_results'].update({n:n+'-result'for n in names})
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/sorceress-spell-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
