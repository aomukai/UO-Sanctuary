#!/usr/bin/env python3
"""General weapon known components and explicit unknown totals with retained contact/proc/input saves."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/gear-strike-link'
def configuration():
    families=('defiler-health-link','hellslayer-readiness','gem-activation','weapon-landed','skill-damage-event','weapon-leech','player-basic-proc')
    connected=('defiler-health-link-check','defiler-health-link-reload-check')
    files={'skill-resistance-cases.json':ROOT/'.local/class-skills/skill-resistance-source/cases.json','weapon-leech-source-probe.json':ROOT/'.local/class-skills/weapon-leech-source-probe.json','hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'}
    inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/elite-damage-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_families=families,extra_files=files,extra_inputs=tuple(dict.fromkeys((*inputs,*helpers))),extra_connected_checks=connected,extra_wait_results={n:n+'-result'for n in connected},extra_connection_settle=.15,extra_manifest_names={'gem-activation':'gem-activation.json'},extra_checks=('sacrifice-contact-check','vengeance-contact-check'),extra_target_input_commands=('player-basic-proc-left','player-basic-proc-right'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
