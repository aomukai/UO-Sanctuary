# Ring of Fire missile-age proposal

Reference:D2R Reign of the Warlock3.3.93847. **Pending user decision;not an implemented live frame-selection rule.**

The primary missile689 formula is `100+(fnum*par1)`,with Param1=5 and lifetime11 updates. The existing captured scaling body operates on an already-rolled raw packet with float32 multiplication/truncation before ordinary mitigation. Supplied frame values0..11 now have36 current-instruction/native comparisons. Free-flight motion separately reuses the existing Nova projection.

The current evaluator's65-byte callback at2bb350 is now captured and linked by the retained evaluator's LEA/call edge. It forwards raw context to parameter helper2d5730. All1,308 bytes of that helper remain protected in the bounded observation;no inaccessible page was read. Thus the exact live binding of `fnum` is still unverified. The current damage table's generic dispatcher/index linkage remains separate as previously documented.

Proposed explicit Sanctuary adaptation:

- `fnum=0` at missile creation.
- Each40ms missile update increments it once,before that update's collision processing. Initial factory path setup adds no extra frame.
- Apply `100+5*fnum` percent to the rolled packet before defender mitigation,using the existing current float32 scaling kernel.
- First update therefore uses105%;the11th terminal update uses155%. This defines damage growth even if motion has slowed or stopped;it follows elapsed updates rather than measured travel distance.

The prepared API currently requires a supplied frame and never silently chooses this interpretation. Approval would resolve this one reference gap;collision/explosion,hit-delay identity,immune knockback and full world/cast admission still need completion. Current handler coverage remains212/240,Warlock3/30. Other work can continue while unanswered.
