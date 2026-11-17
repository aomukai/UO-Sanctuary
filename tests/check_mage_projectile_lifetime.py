#!/usr/bin/env python3
"""Approved Mage death lifetime with focused retained emitter checks."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_necromancer_planned_acceptance import planned_configuration
STAGE=ROOT/'.local/class-skills/mage-projectile-lifetime'
def configuration():
    names=('mage-projectile-lifetime-check','mage-projectile-lifetime-reload-check')
    files=dict(planned_configuration()['extra_files']);files['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    inputs=(Path(__file__).resolve(),ROOT/'tools/reference/mage-projectile-lifetime-approval.json',ROOT/'tools/reference/necromancer-requalification-native-evidence.json')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_families=('mage-projectile-lifetime','hellslayer-readiness','mage-basics','mage-missile','mage-chain','mage-glacial','mage-meteor','mage-pet-buff','mage-world','mage-historical'),extra_files=files,extra_inputs=tuple(dict.fromkeys((*inputs,*helpers))),extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.15,extra_live_commands=('mage-basics-live','mage-glacial-live','mage-chain-live','mage-meteor-live','mage-historical-live'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
