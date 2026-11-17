#!/usr/bin/env python3
"""Requalify the existing Amazon class and new shared contacts on the same binaries."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_amazon_planned_acceptance import configuration as original
from check_corpse_shatter_guard import configuration as contacts
STAGE=ROOT/'.local/class-skills/amazon-current-acceptance'
def union(*groups):return tuple(dict.fromkeys(x for group in groups for x in group))
def configuration():
    a=original();b=contacts();c=dict(b);c['stage']=STAGE
    for key in ('extra_files','extra_wait_results','extra_manifest_names','extra_input_timeouts','extra_check_timeouts'):
        left=a.get(key,{});right=b.get(key,{})
        for name in left.keys()&right.keys():
            if key=='extra_files':assert left[name].read_bytes()==right[name].read_bytes(),name
            else:assert left[name]==right[name],(key,name,left[name],right[name])
        c[key]={**left,**right}
    for key in ('extra_families','extra_checks','extra_target_input_commands','extra_ground_input_commands','extra_connected_checks','extra_inputs','extra_pre_save_commands','extra_pre_save_callbacks','extra_early_reload_families','extra_one_reload_families','extra_save_results','extra_live_commands','checked_live_commands','extra_instant_input_commands'):
        c[key]=union(a.get(key,()),b.get(key,()))
    # Existing class scenes require Fend before the older shared melee anchors.
    assert c['extra_families'].index('fend')<c['extra_families'].index('jab')
    # Bind the final proc/world save witness before Valkyrie's existing seal.
    c['extra_pre_save_commands']=('player-item-proc-world-save-check','valkyrie-initializer-seal')
    # The historical callback's Decoy manifest default points at its historical
    # stage. Bind only the destination;retain its real input and expiry checks.
    def class_wire(session,port,boot):
        from check_amazon_planned_acceptance import LEGACY_INPUTS,old_cast,decoy_checks
        from check_class_skills import fixture_connection
        import time
        count=0;next_connect=time.monotonic()+1.1
        for name in LEGACY_INPUTS:
            remaining=next_connect-time.monotonic()
            if remaining>0:time.sleep(remaining)
            next_connect=time.monotonic()+1.1
            with fixture_connection(session,port)as client:
                time.sleep(.15);count+=old_cast(session,client,name)
        count+=decoy_checks(session,port,boot,stage=STAGE)
        session.send('plague-javelin-snapshot-check')
        assert session.expect(r'plague-javelin-snapshot-check: (passed|failed)').group(1)=='passed'
        return count+1
    c['extra_wire_callbacks']=(class_wire,)
    c['extra_connection_settle']=max(a.get('extra_connection_settle',0),b.get('extra_connection_settle',0))
    c['extra_inputs']=union(c['extra_inputs'],(Path(__file__).resolve(),ROOT/'tools/reference/amazon-planned-acceptance-native-evidence.json',ROOT/'tools/reference/corpse-shatter-guard-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=union(c['extra_inputs'],helpers)
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
