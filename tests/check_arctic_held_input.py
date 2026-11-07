#!/usr/bin/env python3
"""Approved Arctic input,retained Inferno and separately connected save owners."""
import argparse
import os
import socket
import struct
import threading
import time
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_arctic_potion import configuration as prior

OPEN=[]


def drain(client):
    try:
        while client.recv(65536):pass
    except OSError:pass


def wires(session,port,boot):
    count=0
    for prefix in ['inferno','arctic']:
        for source in [0,1,9]:
            label=f'{prefix}-held-wire-{source}'
            with socket.create_connection(('127.0.0.1',port),timeout=5) as client:
                threading.Thread(target=drain,args=(client,),daemon=True).start()
                session.send(label+'-check')
                ready=session.expect(label+r': ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
                ability,x,y,z=map(int,ready.groups());sequence=0
                def send(action,input_source=source,replay=False):
                    nonlocal sequence
                    if not replay:sequence+=1
                    if action==11:packet=struct.pack('>BHHBBIB',0xBF,12,0xF2,7,action,sequence,input_source)
                    else:packet=struct.pack('>BHHBBIBBIHHb',0xBF,22,0xF2,7,action,sequence,input_source,ability,0,x,y,z)
                    client.sendall(packet)
                send(10);send(9);send(9,replay=True);send(9)
                session.expect(label+': pressed')
                for _ in range(4):time.sleep(.15);send(10)
                session.expect(label+': burning')
                send(11,input_source=(source+1)%14);send(10);send(11);send(10)
                assert session.expect(label+r'-result: (passed|failed)',timeout=10).group(1)=='passed'
            time.sleep(.1);count+=1
        # Each saved caster gets a distinct socket retained through the real
        # world save. The prior cohort's own connected save remains intact.
        client=socket.create_connection(('127.0.0.1',port),timeout=5);OPEN.append(client)
        threading.Thread(target=drain,args=(client,),daemon=True).start()
        command=prefix+'-held-input-save-stage-check';session.send(command)
        assert session.expect(command+r': (passed|failed)',timeout=30).group(1)=='passed'
        count+=1
    return count


def configuration(retained=False,isolate=False,shared=False):
    c=prior(retained,isolate,shared)
    suffix='-shared'if shared else'-isolate'if isolate else'-retained'if retained else''
    c['stage']=ROOT/('.local/class-skills/arctic-held-input'+suffix)
    c['extra_families']+=('inferno-held-input','arctic-held-input')
    c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'inferno-held-input-check','inferno-held-input-reload-check','arctic-held-input-check','arctic-held-input-reload-check')
    c['extra_connection_settle']=max(c.get('extra_connection_settle',0),.15)
    c['extra_wire_callbacks']=(*c.get('extra_wire_callbacks',()),wires)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'docs/arctic-approved-input-rule.md')
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:check(**configuration(a.retained,a.isolate,a.shared))
    finally:
        for client in OPEN:client.close()
