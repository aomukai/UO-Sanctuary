#!/usr/bin/env python3
"""All five stationary sentry missile impacts retain typed Health Link context."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/sentry-missile-link'
LEARNED=('sentry-learned','lightning-learned','inferno-learned','wake-learned','death-learned')
FAMILIES=('defiler-health-link','hellslayer-readiness','sentry-bolt','lightning-sentry','inferno-sentry','wake-fire-missile','death-control',*LEARNED)
def configuration():
    names=tuple(n+s for n in ('defiler-health-link',*LEARNED)for s in ('-check','-reload-check'))
    waits={n:(n+'-result'if n.startswith('defiler')else n.removesuffix('-check')+'-result')for n in names}
    inputs=tuple(n+'-'+m for n in LEARNED for m in ('left','right','quick'))
    c=dict(stage=STAGE,extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=FAMILIES,extra_checks=('sentry-learned-availability-check',*(n+'-mechanics-check'for n in LEARNED)),extra_wait_results=waits,extra_connected_checks=names,extra_connection_settle=.15,extra_target_input_commands=inputs,extra_ground_input_commands=inputs,extra_instant_input_commands=tuple(n+'-left'for n in LEARNED),extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/sentry-explosion-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py'))
    for n in ('death-sentry-ai','death-sentry-corpse-search','corpse-explosion','death-sentry-clock','lightning-sentry-damage','inferno-sentry-controller','wake-of-inferno-damage','wake-of-fire-clock','wake-of-fire-damage','wake-of-fire-factory'):c['extra_files'][n+'-source-probe.json']=ROOT/'.local/class-skills'/(n+'-source-probe.json')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
