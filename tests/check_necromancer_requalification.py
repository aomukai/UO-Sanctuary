#!/usr/bin/env python3
"""Current Necromancer combined qualification after shared damage repairs.

Reuse exact accepted binaries and cached pinned-reference cases. Preserve the
original cohort, then add the current shared Health Link and readiness scopes.
"""
import os,sys
from pathlib import Path
from check_necromancer_planned_acceptance import planned_configuration
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/necromancer-requalification'
def configuration():
    c=planned_configuration();c['stage']=STAGE
    c['extra_families']=tuple(dict.fromkeys((*c['extra_families'],'defiler-health-link','hellslayer-readiness')))
    names=('defiler-health-link-check','defiler-health-link-reload-check')
    c['extra_connected_checks']=tuple(dict.fromkeys((*c['extra_connected_checks'],*names)))
    c['extra_wait_results'].update({n:n+'-result'for n in names});c['extra_connection_settle']=.15
    c['extra_files']['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    extra=(Path(__file__).resolve(),ROOT/'tools/reference/gear-strike-link-native-evidence.json',ROOT/'tools/reference/defiler-health-link-rule-approval.json')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*extra,*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
