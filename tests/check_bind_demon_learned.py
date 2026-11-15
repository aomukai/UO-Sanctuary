#!/usr/bin/env python3
"""Approved Bind with real right/quick hold packets and original native saves."""
import os,socket,struct,threading,time
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def wires(session,port,boot):
    for source in (1,9):
        label=f'bind-learned-wire-{source}'
        with socket.create_connection(('127.0.0.1',port),timeout=5)as client:
            def drain():
                try:
                    while client.recv(65536):pass
                except OSError:pass
            threading.Thread(target=drain,daemon=True).start();session.send(label+'-check')
            ready=session.expect(label+r': ready ability=(\d+) target=(\d+) other=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
            ability,target,other,x,y,z=map(int,ready.groups());sequence=0
            def send(action,input_source=source,chosen=target,replay=False):
                nonlocal sequence
                if not replay:sequence+=1
                if action in(0,2):packet=struct.pack('>BHHBBI',0xBF,11,0xF2,7,action,sequence)
                elif action==11:packet=struct.pack('>BHHBBIB',0xBF,12,0xF2,7,action,sequence,input_source)
                elif action==3:packet=struct.pack('>BHHBBIBIHHb',0xBF,21,0xF2,7,action,sequence,ability,chosen,x,y,z)
                else:packet=struct.pack('>BHHBBIBBIHHb',0xBF,22,0xF2,7,action,sequence,input_source,ability,chosen,x,y,z)
                client.sendall(packet)
            send(0);send(10);send(9,input_source=0);send(3);send(2);send(9);send(9,replay=True);send(9)
            session.expect(label+': pressed-free');time.sleep(.15);send(10,chosen=other);send(11,input_source=9 if source==1 else 1);send(10,chosen=other);send(11)
            session.expect(label+': released-free');send(10,chosen=other);send(2);send(9);send(9)
            session.expect(label+': pressed')
            for _ in range(17):time.sleep(.15);send(10,chosen=other)
            if source==1:session.expect(label+': healthy-paid')
            session.expect(label+': bound',timeout=5);send(11,input_source=9 if source==1 else 1);send(10,chosen=other);send(11);send(10,chosen=other)
            time.sleep(.15);session.send(label+'-finish');assert session.expect(label+r'-result: (passed|failed)',timeout=10).group(1)=='passed'
    return 2

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    connected=('bind-demon-host-audit','bind-demon-input','bind-demon-learned')
    names=tuple(n for f in connected for n in(f+'-check',f+'-reload-check'))
    check(stage=ROOT/'.local/class-skills/bind-demon-learned',extra_families=connected,extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,extra_wire_callbacks=(wires,),
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bind-demon-world-rule-approval.json'))
