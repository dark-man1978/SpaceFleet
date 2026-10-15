using Godot;
using System;
using System.Collections.Generic;

namespace SpaceFleet;

public partial class SoundSynth : Node
{
    private sealed class Voice { public float Freq,Left,Duration,Volume; public bool Noise; public double Phase; }
    private readonly List<Voice> voices=new();
    private AudioStreamPlayer player=null!;
    private AudioStreamGeneratorPlayback? playback;
    private uint noise=341;
    private double enginePhase;
    public float Engine;
    public bool Muted;
    public override void _Ready()
    {
        var stream=new AudioStreamGenerator { MixRate=22050,BufferLength=.15f };
        player=new AudioStreamPlayer { Stream=stream };AddChild(player);player.Play();
        playback=player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }
    public void Tone(float frequency,float duration,float volume,bool noisy=false)
    {
        if(voices.Count<18)voices.Add(new Voice {Freq=frequency,Duration=duration,Left=duration,Volume=volume,Noise=noisy});
    }
    public override void _Process(double delta)
    {
        if(playback==null)return;
        int frames=playback.GetFramesAvailable();
        for(int i=0;i<frames;i++)
        {
            enginePhase+=(45+Engine*75)/22050.0;
            float value=(float)Math.Sin(enginePhase*Math.Tau)*Engine*.025f;
            for(int j=voices.Count-1;j>=0;j--)
            {
                var v=voices[j];v.Left-=1f/22050;
                if(v.Left<=0){voices.RemoveAt(j);continue;}
                v.Phase+=v.Freq*(.4+v.Left/v.Duration*.6)/22050.0;
                noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;
                float n=(noise%65536)/32768f-1;
                value+=((float)Math.Sin(v.Phase*Math.Tau)*(v.Noise?.6f:1)+(v.Noise?n*.4f:0))*v.Volume*(v.Left/v.Duration);
            }
            value=Muted?0:Mathf.Clamp(value,-.9f,.9f);
            playback.PushFrame(new Vector2(value,value));
        }
    }
}
