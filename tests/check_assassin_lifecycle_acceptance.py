#!/usr/bin/env python3
"""Current Assassin inputs, original saves and shared weapon/spell/link contracts."""
import argparse,os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import configuration as previous,merge,paced_clients,OPEN
from check_assassin_weapon_contact import configuration as weapon
from check_assassin_spell_link import configuration as spells
from check_martial_element_link import configuration as martial
from check_blade_link import configuration as blades
import check_sentinel_stealskull_link as actual
STAGE=ROOT/'.local/class-skills/assassin-lifecycle-acceptance'

def configuration():
    c=previous();c['stage']=STAGE
    for build in (weapon,spells,martial,blades):
        other=build();merge(c,other)
        for key in ('extra_pre_save_commands','extra_early_reload_families'):
            c[key]=tuple(dict.fromkeys((*c.get(key,()),*other.get(key,()))))
    c['extra_families']+=('cold-corpse-proposal',)
    # Co-op scenes need both sockets alone; Fury's existing save witness
    # deliberately keeps its own connection alive until the world save.
    actual.STAGE=STAGE;c['extra_wire_callbacks']=(actual.wires,*c['extra_wire_callbacks'])
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),*helpers,
        ROOT/'tools/reference/sentinel-stealskull-link-native-evidence.json',ROOT/'tools/reference/ordinary-cold-corpse-approval.json')))
    for key in ('extra_families','extra_checks','extra_target_input_commands'):
        assert len(c[key])==len(set(c[key])),key
    return c

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',action='store_true');args=p.parse_args();c=configuration()
    if args.plan:print({k:len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files')})
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        try:
            with paced_clients():check(**c)
        finally:
            for client in OPEN:client.close()
