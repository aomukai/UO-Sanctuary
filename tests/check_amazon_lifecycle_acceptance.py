#!/usr/bin/env python3
"""Current Amazon class plus adopted Pierce/cold/Impale and settled-damage scopes."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
import check_amazon_current_acceptance as current
from check_amazon_remaining_input import INPUTS
from check_pierce_action_adoption import configuration as pierce
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/amazon-lifecycle-acceptance'

def configuration():
    # The retained callback resolves its manifest destination through this
    # module global. Keep historical stage files untouched without cloning it.
    current.STAGE=STAGE;c=current.configuration();c['stage']=STAGE;p=pierce()
    c['extra_families']+=('amazon-sequence-contact','impale-contact','amazon-thrust-contact','pierce-action','cold-corpse-proposal','defiler-health-link')
    c['extra_checks']+=('pierce-seed-check',)
    for n,path in p['extra_files'].items():
        if n in c['extra_files']:assert c['extra_files'][n].read_bytes()==path.read_bytes(),n
        c['extra_files'][n]=path
    c['extra_target_input_commands']+=(*INPUTS,*p['extra_target_input_commands'])
    connected=('defiler-health-link-check','defiler-health-link-reload-check')
    c['extra_connected_checks']+=connected;c['extra_wait_results'].update({n:n+'-result'for n in connected})
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*p['extra_inputs'],Path(__file__).resolve(),
        ROOT/'tools/reference/impale-defense-policy-approval.json',ROOT/'tools/reference/ordinary-cold-corpse-approval.json')))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    assert len(c['extra_families'])==81 and len(c['extra_target_input_commands'])==65
    for path in (*c['extra_inputs'],*c['extra_files'].values()):assert path.is_file(),path
    return c

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
