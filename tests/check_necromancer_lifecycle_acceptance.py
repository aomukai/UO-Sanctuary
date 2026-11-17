#!/usr/bin/env python3
"""Current combined Necromancer cohort with approved Mage/corpse lifecycles."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_necromancer_requalification import configuration as prior
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/necromancer-lifecycle-acceptance'
def configuration():
    c=prior();c['stage']=STAGE
    c['extra_families']=tuple(dict.fromkeys((*c['extra_families'],'mage-projectile-lifetime','cold-corpse-proposal','corpse-shatter-guard')))
    names=('mage-projectile-lifetime-check','mage-projectile-lifetime-reload-check')
    c['extra_connected_checks']=tuple(dict.fromkeys((*c['extra_connected_checks'],*names)))
    c['extra_wait_results'].update({n:n+'-result'for n in names})
    c['extra_wait_results'].update({'corpse-shatter-guard-check':'corpse-shatter-guard-result','corpse-shatter-guard-reload-check':'corpse-shatter-guard-result'})
    c['extra_inputs']+=tuple(ROOT/p for p in ('tests/check_necromancer_lifecycle_acceptance.py','tools/reference/ordinary-cold-corpse-approval.json','tools/reference/ordinary-cold-corpse-adoption-native-evidence.json','tools/reference/mage-projectile-lifetime-native-evidence.json','tools/reference/necromancer-requalification-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)));return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
