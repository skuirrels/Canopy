export function createBuildSound(){
  let audio: AudioContext | undefined;
  let mix: GainNode, volume: GainNode, bed: GainNode, room: ConvolverNode, noise: AudioBuffer;
  let enabled=false, suspendTimer: ReturnType<typeof setTimeout> | undefined;
  const voices=new Set<AudioScheduledSourceNode>();
  function initialise(){
    if(audio)return;
    const AudioCtor=window.AudioContext;
    if(!AudioCtor)throw new Error('Audio is unavailable in this browser.');
    audio=new AudioCtor();
    mix=audio.createGain();
    const high=audio.createBiquadFilter();high.type='highpass';high.frequency.value=35;high.Q.value=.5;
    const low=audio.createBiquadFilter();low.type='lowpass';low.frequency.value=3000;low.Q.value=.5;
    const limiter=audio.createDynamicsCompressor();limiter.threshold.value=-18;limiter.knee.value=12;limiter.ratio.value=5;limiter.attack.value=.004;limiter.release.value=.2;
    volume=audio.createGain();volume.gain.value=0;
    mix.connect(high).connect(low).connect(limiter).connect(volume).connect(audio.destination);
    noise=audio.createBuffer(1,audio.sampleRate*2,audio.sampleRate);
    const data=noise.getChannelData(0);let randomState=731;
    function noiseValue(){randomState=(randomState*16807)%2147483647;return(randomState/2147483647)*2-1;}
    for(let i=0;i<data.length;i++)data[i]=noiseValue();
    room=audio.createConvolver();
    const impulse=audio.createBuffer(2,Math.ceil(audio.sampleRate*.42),audio.sampleRate);
    for(let c=0;c<2;c++){const channel=impulse.getChannelData(c);for(let i=0;i<channel.length;i++)channel[i]=noiseValue()*Math.exp(-i/(audio.sampleRate*.075))*.28;}
    room.buffer=impulse;const wet=audio.createGain();wet.gain.value=.2;room.connect(wet).connect(mix);
    bed=audio.createGain();bed.gain.value=0;bed.connect(mix);
    const context=audio;
    ([[55,.45,'triangle',-2],[110,.18,'sine',2],[165,.045,'triangle',0]] as [number,number,OscillatorType,number][]).forEach(([frequency,level,type,detune])=>{
      const oscillator=context.createOscillator(),gain=context.createGain();
      oscillator.type=type;oscillator.frequency.value=frequency;oscillator.detune.value=detune;gain.gain.value=level;
      oscillator.connect(gain).connect(bed);oscillator.start();
    });
  }
  function ramp(param: AudioParam,target: number,seconds: number){if(!audio)return;const now=audio.currentTime;if(param.cancelAndHoldAtTime)param.cancelAndHoldAtTime(now);else{param.cancelScheduledValues(now);param.setValueAtTime(param.value,now);}param.linearRampToValueAtTime(target,now+seconds);}
  function ready(){return enabled&&audio?.state==='running'&&!document.hidden;}
  function envelope(duration: number,level: number,attack: number){if(!audio)throw new Error("Audio is not initialized.");const gain=audio.createGain(),t=audio.currentTime;gain.gain.setValueAtTime(0,t);gain.gain.linearRampToValueAtTime(level,t+attack);gain.gain.linearRampToValueAtTime(level*.65,t+Math.max(attack,duration*.48));gain.gain.linearRampToValueAtTime(0,t+duration);return gain;}
  function track(source: AudioScheduledSourceNode,nodes: AudioNode[],duration: number){if(!audio)return;voices.add(source);source.onended=()=>{voices.delete(source);source.disconnect();nodes.forEach(node=>node.disconnect());};source.start();source.stop(audio.currentTime+duration+.02);}
  function note(frequency: number,duration: number,level=.07,attack=.08){
    if(!ready()||!audio)return;
    const oscillator=audio.createOscillator(),gain=envelope(duration,level,attack),filter=audio.createBiquadFilter();
    oscillator.type='triangle';oscillator.frequency.value=frequency;
    filter.type='lowpass';filter.frequency.value=850;filter.Q.value=.45;
    oscillator.connect(filter).connect(gain);gain.connect(mix);gain.connect(room);track(oscillator,[filter,gain],duration);
  }
  function air(duration=.5,level=.12,from=450,to=1300){
    if(!ready()||!audio)return;
    const source=audio.createBufferSource(),filter=audio.createBiquadFilter(),gain=envelope(duration,level,Math.min(.1,duration*.3));
    source.buffer=noise;filter.type='bandpass';filter.Q.value=.45;
    filter.frequency.setValueAtTime(from,audio.currentTime);filter.frequency.linearRampToValueAtTime(to,audio.currentTime+duration);
    source.connect(filter).connect(gain).connect(mix);track(source,[filter,gain],duration);
  }
  function hum(running: boolean){if(audio)ramp(bed.gain,enabled && running && !document.hidden ? .12 : 0,.45);}
  return {
    get enabled(){return enabled;},
    async setEnabled(value: boolean){
      clearTimeout(suspendTimer);
      if(value){initialise();if(!audio)throw new Error("Audio is unavailable.");await audio.resume();if(audio.state!=='running')throw new Error('Audio playback could not start.');enabled=true;ramp(volume.gain,.42,.12);}
      else {enabled=false;if(audio){ramp(volume.gain,0,.08);hum(false);suspendTimer=setTimeout(()=>{if(!enabled){voices.forEach(source=>{try{source.stop();}catch{}});void audio?.suspend().catch(()=>{});}},120);}}
    },
    hum,
    preview(){note(73.416,.8,.14,.12);air(.65,.1,300,1050);},
    mechanical(){air(.18,.08,750,1150);},
    attention(){hum(false);note(82.407,.65,.08,.1);note(110,.65,.035,.1);},
    complete(){hum(false);air(.7,.09,800,350);[146.832,220,293.665].forEach(f=>note(f,1.5,.065,.16));},
    close(){enabled=false;clearTimeout(suspendTimer);voices.forEach(source=>{try{source.stop();}catch{}});if(audio&&audio.state!=='closed')void audio.close().catch(()=>{});}
  };
}

export type BuildSound = ReturnType<typeof createBuildSound>;
