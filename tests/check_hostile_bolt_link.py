#!/usr/bin/env python3
"""Hostile firebolt known components with retained actual producer/defense/save paths."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/hostile-bolt-link'
def configuration():
    families=('defiler-health-link','hellslayer-readiness','hostile-firebolt','slow-missiles-rules','cyclone-armor')
    connected=tuple(f+s for f in ('defiler-health-link','slow-missiles-rules')for s in ('-check','-reload-check'))
    wait={n:n+'-result'for n in connected};wait.update({n:'hostile-firebolt-result'for n in ('hostile-firebolt-check','hostile-firebolt-reload-check')})
    files={'skill-resistance-cases.json':ROOT/'.local/class-skills/skill-resistance-source/cases.json','cyclone-armor-source-probe.json':ROOT/'.local/class-skills/cyclone-armor-source-probe.json','hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'}
    inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/warlock-direct-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_families=families,extra_files=files,extra_inputs=tuple(dict.fromkeys((*inputs,*helpers))),extra_connected_checks=connected,extra_wait_results=wait,extra_connection_settle=.15)
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
