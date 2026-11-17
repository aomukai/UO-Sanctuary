#!/usr/bin/env python3
"""Current Druid lifecycle, input and shared item/damage integration."""
import argparse,os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_druid_pet_link import configuration as previous
from check_druid_planned_acceptance import merge,OPEN
from check_druid_weapon_poison import configuration as weapon
from check_armageddon_link import configuration as armageddon
from check_unresisted_shields import configuration as shields
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/druid-lifecycle-acceptance'

def configuration():
    c=previous();c['stage']=STAGE
    for build in (weapon,armageddon,shields):
        other=build();merge(c,other)
        for key in ('extra_pre_save_commands','extra_pre_save_callbacks','extra_early_reload_families','extra_instant_input_commands'):
            c[key]=tuple(dict.fromkeys((*c.get(key,()),*other.get(key,()))))
    # The existing elemental wire callback already includes Armageddon.
    # Retain that scenario once; the added live check covers natural weather.
    c['extra_families']+=('cold-corpse-proposal',)
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),*helpers,
        ROOT/'tools/reference/ordinary-cold-corpse-approval.json',ROOT/'tools/reference/assassin-lifecycle-acceptance-native-evidence.json')))
    for key in ('extra_families','extra_checks','extra_target_input_commands','extra_pre_save_commands'):
        assert len(c[key])==len(set(c[key])),key
    return c

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',action='store_true');args=p.parse_args();c=configuration()
    if args.plan:print({k:len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files','extra_pre_save_commands')})
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        try:
            with paced_clients():check(**c)
        finally:
            for client in OPEN:client.close()
