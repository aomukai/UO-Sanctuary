#!/usr/bin/env python3
"""Current Barbarian inputs, original saves, contacts and War Cry integrations."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_barbarian_planned_acceptance import configuration as prior
from check_barbarian_remaining_input import INPUTS
from check_barbarian_contact import worker_snapshot
from check_assassin_planned_acceptance import paced_clients

def configuration():
    c=prior();c['stage']=ROOT/'.local/class-skills/barbarian-lifecycle-acceptance'
    c['extra_families']+=('barbarian-contact','warcry-potion','defiler-health-link','hellslayer-readiness')
    c['extra_files']['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    c['extra_target_input_commands']+=INPUTS
    connected=('defiler-health-link-check','defiler-health-link-reload-check')
    c['extra_connected_checks']+=connected
    c['extra_wait_results'].update({n:n+'-result'for n in connected})
    c['extra_pre_save_callbacks']=(*c.get('extra_pre_save_callbacks',()),worker_snapshot)
    c['extra_connection_settle']=.15
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),*helpers,
        ROOT/'tools/reference/warcry-potion-native-evidence.json',ROOT/'tools/reference/barbarian-contact-source-review.json',
        ROOT/'tools/reference/defiler-health-link-rule-approval.json')))
    assert len(c['extra_target_input_commands'])==48
    for p in (*c['extra_inputs'],*c['extra_files'].values()):assert p.is_file(),p
    return c

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
