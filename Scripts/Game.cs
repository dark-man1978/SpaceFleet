using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpaceFleet;

public partial class Game : Node3D
{
    public readonly List<ShipUnit> Units=new();
    public readonly List<Projectile> Shots=new();
    public RandomNumberGenerator Rng=new();
    public ShipUnit Player=null!;
    public ShipUnit? EnemyCarrier;
    public Camera3D Camera=null!;
    public Hud Hud=null!;
    public SoundSynth Sound=null!;
    public MissionPlan? Plan;
    public GamePhase Phase=GamePhase.Menu;
    public int NavIndex,Kills;
    public int Difficulty=1;
    public float MissionTime,HitFlash,RadioTime,LockBeepTimer;
    public bool Returning,Cruise,Paused;
    public string Message="",WingOrder="SCHUTZ",ResultText="";
    public bool Success;
    public Vector3 Home=new(0,0,500);
    public Vector3 NavPosition=>Returning?Home+new Vector3(0,50,-200):Plan!=null&&NavIndex<Plan.Points.Count?Plan.Points[NavIndex].Position:Home;
    public float NavDistance=>Player==null?0:Player.GlobalPosition.DistanceTo(NavPosition);
    public int BestKills,Completed;
    public float BestTime;
    public bool Muted;
    private Node3D actors=null!,universe=null!,cockpit=null!,homeModel=null!;
    private float cinematic,menuTime,stageClearTimer,damageWarning;
    private bool outcomeRecorded;
    public override void _Ready()
    {
        Database.Load();Controls.Install();Rng.Randomize();LoadProfile();
        universe=new Node3D();AddChild(universe);Visuals.Universe(universe,7642);
        homeModel=Visuals.Carrier(universe,false);homeModel.Position=Home;
        Sound=new SoundSynth();AddChild(Sound);Sound.Muted=Muted;
        Camera=new Camera3D { Fov=80,Near=.08f,Far=100000,Current=true };AddChild(Camera);
        actors=new Node3D();AddChild(actors);
        cockpit=Visuals.Cockpit(Camera);cockpit.Visible=false;
        var layer=new CanvasLayer();AddChild(layer);Hud=new Hud { Game=this };layer.AddChild(Hud);
        Hud.ShowMenu();
        if(OS.GetCmdlineUserArgs().Contains("--self-test"))CallDeferred(nameof(RunSelfTests));
        if(OS.GetCmdlineUserArgs().Contains("--smoke-test")){Prepare(MissionKind.Strike);StartLaunch();}
        foreach(string arg in OS.GetCmdlineUserArgs())if(arg.StartsWith("--capture="))CallDeferred(nameof(CaptureQa),arg[10..]);
    }
    public void Prepare(MissionKind kind,int? seed=null)
    {
        Plan=MissionPlan.Generate(kind,seed??(int)Rng.RandiRange(1,int.MaxValue-1),Difficulty);
        Phase=GamePhase.Briefing;Hud.ShowBriefing();
    }
    public void StartLaunch()
    {
        ClearActors();NavIndex=Kills=0;Returning=Cruise=Paused=false;MissionTime=0;stageClearTimer=0;
        EnemyCarrier=null;WingOrder="SCHUTZ";outcomeRecorded=false;HitFlash=0;
        foreach(var nav in Plan!.Points){nav.Activated=false;nav.Cleared=false;}
        Player=Spawn(Plan.ShipId,true,Home+new Vector3(0,42,150),true);
        Player.Model=Visuals.Fighter(Player,Plan.ShipId,false);
        Player.Callsign="Spectre 1";Player.Throttle=.6f;
        if(Plan.Kind==MissionKind.Escort)
        {
            var a=Spawn("bomber",true,Home+new Vector3(-90,65,-450));a.Callsign="Atlas 2";
            var b=Spawn("bomber",true,Home+new Vector3(90,65,-520));b.Callsign="Atlas 3";
        }
        else
        {
            var a=Spawn("light",true,Home+new Vector3(-90,65,-420));a.Callsign="Kestrel 2";
            var b=Spawn("light",true,Home+new Vector3(90,65,-480));b.Callsign="Kestrel 3";
        }
        cinematic=0;Phase=GamePhase.Launch;cockpit.Visible=false;Hud.ClearMenu();
        Radio("Kontrolle: Spectre 1, Start freigegeben. Guten Flug.");
        Sound.Tone(90,1,.15f,true);
    }
    public ShipUnit Spawn(string id,bool friendly,Vector3 pos,bool player=false)
    {
        var s=new ShipUnit();actors.AddChild(s);s.Position=pos;s.Initialize(this,id,friendly,player);Units.Add(s);return s;
    }
    private void ClearActors()
    {
        foreach(Node n in actors.GetChildren())n.QueueFree();Units.Clear();Shots.Clear();
    }
    public void BackToMenu()
    {
        ClearActors();cockpit.Visible=false;Paused=false;Cruise=false;Phase=GamePhase.Menu;Sound.Engine=0;Hud.ShowMenu();
    }
    public override void _Process(double delta)
    {
        float dt=Mathf.Min((float)delta,.05f);menuTime+=dt;
        if(Phase==GamePhase.Menu||Phase==GamePhase.Briefing||Phase==GamePhase.Debrief)
        {
            Vector3 view=Home+new Vector3(Mathf.Sin(menuTime*.035f)*240+430,200,540);
            Camera.GlobalPosition=view;Camera.LookAt(Home+new Vector3(0,20,0));
        }
        if(Input.IsActionJustPressed("pause"))
        {
            if(Phase==GamePhase.Flight){Paused=!Paused;if(Paused)Hud.ShowPause();else Hud.ClearMenu();}
            else if(Phase==GamePhase.Briefing)BackToMenu();
        }
        Hud.QueueRedraw();
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt=(float)delta;
        if(Paused){Sound.Engine=0;return;}
        RadioTime=Mathf.Max(0,RadioTime-dt);HitFlash=Mathf.Max(0,HitFlash-dt);
        if(Phase==GamePhase.Launch){TickLaunch(dt);return;}
        if(Phase==GamePhase.Landing){TickLanding(dt);return;}
        if(Phase!=GamePhase.Flight)return;
        MissionTime+=dt;
        if(!Player.Alive){Finish(false,"Dein Schiff wurde zerstört.");return;}
        if(Player.Target is {Alive:false}){Player.Target=null;Player.Lock=0;}
        if(Input.IsActionJustPressed("enemy"))CycleTarget();
        if(Input.IsActionJustPressed("target"))SelectInSight();
        if(Input.IsActionJustPressed("nav"))Radio(Returning?"Navigation: Rückkehr zur SFS Resolute.":$"Navigation: {Plan!.Points[NavIndex].Name} markiert.");
        if(Input.IsActionJustPressed("cruise"))
        {
            if(NearestOpponent(Player,4200)!=null)Radio("Reiseflug gesperrt: Feinde in der Nähe.");
            else {Cruise=!Cruise;Radio(Cruise?"Reiseflug aktiv. R3 / J zum Beenden.":"Reiseflug beendet.");}
        }
        if(Cruise&&(NearestOpponent(Player,4200)!=null||NavDistance<1050||Input.IsActionPressed("fire")||Input.IsActionPressed("missile")))Cruise=false;
        if(Input.IsActionJustPressed("command_form"))Command("FORMATION");
        if(Input.IsActionJustPressed("command_attack"))Command("ANGRIFF");
        if(Input.IsActionJustPressed("command_protect"))Command("SCHUTZ");
        if(Input.IsActionJustPressed("command_return"))Command("RÜCKKEHR");
        foreach(var ship in Units.ToArray())ship.Tick(dt);
        foreach(var shot in Shots.ToArray())shot.Tick(dt);
        Shots.RemoveAll(s=>!s.Alive);
        // Ship proximity collision; home carrier is handled as a docking zone on return.
        foreach(var u in Units)
        {
            if(!u.Alive||u.Friendly||u.IsGenerator)continue;
            if(Player.GlobalPosition.DistanceTo(u.GlobalPosition)<u.Radius+Player.Radius)
            {
                Player.TakeDamage(140*dt,1,u.GlobalPosition,u);
                var away=(Player.GlobalPosition-u.GlobalPosition).Normalized();
                Player.GlobalPosition=u.GlobalPosition+away*(u.Radius+Player.Radius+2);Player.Speed*=.8f;
            }
        }
        TickMission(dt);
        Camera.GlobalTransform=Player.GlobalTransform;
        Camera.Fov=Mathf.Lerp(Camera.Fov,Input.IsActionPressed("boost")?87:80,dt*3);
        Sound.Engine=Player.Speed/(Player.Spec.Speed*1.85f);
        if(Player.Target is {Alive:true} && Player.Lock>=Database.Weapons[Player.MissileId].LockTime)
        {
            LockBeepTimer-=dt;if(LockBeepTimer<=0){Sound.Tone(950,.04f,.07f);LockBeepTimer=.65f;}
        }
        damageWarning-=dt;
        if(Player.Hull<Player.Spec.Hull*.25f&&damageWarning<=0){Sound.Tone(360,.3f,.15f);damageWarning=3;}
        bool incoming=Shots.Exists(s=>s.Alive&&s.Homing&&s.Target==Player);
        if(incoming){Cruise=false; if(damageWarning<=0){Sound.Tone(800,.1f,.15f);damageWarning=.65f;}}
    }
    private void TickLaunch(float dt)
    {
        cinematic+=dt;
        float t=Mathf.Clamp(cinematic/6,0,1);
        Player.GlobalPosition=Home+new Vector3(0,42+35*t,150-1050*t*t);
        Camera.GlobalPosition=Player.GlobalPosition+new Vector3(42+30*t,20,65);
        Camera.LookAt(Player.GlobalPosition-new Vector3(0,0,10));
        Sound.Engine=.3f+t*.4f;
        if(cinematic>=6||(cinematic>.4f&&Input.IsActionJustPressed("skip")))
        {
            Player.GlobalPosition=Home+new Vector3(0,80,-900);Player.Speed=110;
            Player.Model!.Visible=false;Phase=GamePhase.Flight;cockpit.Visible=true;
            Camera.GlobalTransform=Player.GlobalTransform;
            Radio("Kontrolle: Flugroute übertragen. Steuere NAV 01 an. R3 aktiviert den Reiseflug.");
        }
    }
    private void TickLanding(float dt)
    {
        cinematic+=dt;float t=Mathf.Clamp(cinematic/6,0,1);
        Player.GlobalPosition=Home+new Vector3(0,42,-450+580*t);
        Player.Basis=Basis.Identity;
        Camera.GlobalPosition=Player.GlobalPosition+new Vector3(80,45,-100);Camera.LookAt(Player.GlobalPosition);
        Sound.Engine=(1-t)*.4f;
        if(cinematic>=6||(cinematic>.4f&&Input.IsActionJustPressed("skip")))Finish(true,"Auftrag erfüllt. Willkommen zurück auf der Resolute.");
    }
    private void TickMission(float dt)
    {
        if(Plan!.Kind==MissionKind.Escort&&!Units.Exists(u=>u.Alive&&u.Friendly&&u.Spec.Id=="bomber"))
        {Finish(false,"Beide Bomber wurden zerstört. Eskorte gescheitert.");return;}
        if(MissionTime>1800){Finish(false,"Einsatzzeit überschritten. Mission abgebrochen.");return;}
        if(Returning)
        {
            if(NavDistance<380&&NearestOpponent(Player,2200)==null)
            {
                if(Player.Speed>Player.Spec.Speed*.8f){Radio("Landekontrolle: Geschwindigkeit reduzieren. Schub unter 70 %.");return;}
                Phase=GamePhase.Landing;cinematic=0;Cruise=false;cockpit.Visible=false;Player.Model!.Visible=true;
                Radio("Landekontrolle: Anflug übernommen. Landeerlaubnis erteilt.");
            }
            return;
        }
        var nav=Plan.Points[NavIndex];
        if(!nav.Activated&&NavDistance<750)Activate(nav);
        if(!nav.Activated)return;
        bool remaining=Units.Exists(u=>u.Alive&&!u.Friendly&&!u.IsGenerator);
        if(!remaining)
        {
            stageClearTimer+=dt;
            if(stageClearTimer>2)
            {
                nav.Cleared=true;NavIndex++;stageClearTimer=0;
                if(NavIndex>=Plan.Points.Count)
                {Returning=true;Cruise=false;WingOrder="FORMATION";Radio("Kontrolle: Alle Einsatzziele erfüllt. Kehre zur Resolute zurück.");}
                else Radio($"Gebiet gesichert. Weiter zu {Plan.Points[NavIndex].Name}.");
            }
        }
        else stageClearTimer=0;
        if(Plan.Kind==MissionKind.Strike && EnemyCarrier is {Alive:true} && Player.Torpedoes==0 && !Shots.Exists(s=>s.Alive&&s.Spec.Id=="torpedo"&&s.Shooter==Player)&&MissionTime>30)
        {
            // Energy weapons remain capable of finishing a damaged carrier. No hidden hard failure.
            if(damageWarning<=0){Radio("Keine Torpedos mehr. Schildgeneratoren mit Energiewaffen angreifen.");damageWarning=30;}
        }
    }
    private void Activate(NavPoint nav)
    {
        nav.Activated=true;Cruise=false;stageClearTimer=0;
        if(nav.Encounter==Encounter.Empty){Radio("Sensoren: Keine feindlichen Kontakte. Sektor wird überprüft.");return;}
        int count=nav.Encounter==Encounter.Carrier?2+Plan!.Difficulty:2+Plan!.Difficulty+Rng.RandiRange(0,1);
        if(nav.Encounter==Encounter.Carrier)
        {
            EnemyCarrier=Spawn("carrier",false,nav.Position+new Vector3(0,0,-900));
            for(int i=-1;i<=1;i+=2)
            {
                var g=Spawn("generator",false,EnemyCarrier.GlobalPosition+new Vector3(i*125,66,-110));
                g.ParentCarrier=EnemyCarrier;g.MountOffset=new Vector3(i*125,66,-110);g.Callsign=i<0?"Schildgenerator Backbord":"Schildgenerator Steuerbord";
            }
            Radio("Sensoren: Feindlicher Träger! Bomber zum Angriff. Jäger sichern den Anflug.");
        }
        else Radio($"Sensoren: {count} feindliche Abfangjäger voraus!");
        for(int i=0;i<count;i++)
        {
            var ship=Spawn("enemy",false,nav.Position+new Vector3(Rng.RandfRange(-500,500),Rng.RandfRange(-220,220),-600-i*110));
            ship.LookAt(Player.GlobalPosition);ship.Callsign=$"Raider {i+1}";
            float modifier=Plan!.Difficulty==0?.7f:Plan.Difficulty==2?1.2f:1;
            ship.Hull*=modifier;
        }
        if(Player.Target is not {Alive:true})Player.Target=NearestOpponent(Player,8000);
    }
    public ShipUnit? NearestOpponent(ShipUnit source,float distance)
    {
        ShipUnit? best=null;float d2=distance*distance;
        foreach(var unit in Units)
        {
            if(!unit.Alive||unit.Friendly==source.Friendly||unit.IsGenerator)continue;
            float d=source.GlobalPosition.DistanceSquaredTo(unit.GlobalPosition);
            if(d<d2){d2=d;best=unit;}
        }
        return best;
    }
    private void CycleTarget()
    {
        var targets=Units.Where(u=>u.Alive&&!u.Friendly&&u.GlobalPosition.DistanceTo(Player.GlobalPosition)<10000).OrderBy(u=>u.IsGenerator?1:0).ThenBy(u=>u.GetInstanceId()).ToList();
        if(targets.Count==0){Player.Target=null;Radio("Keine feindlichen Kontakte.");return;}
        int index=Player.Target==null?-1:targets.IndexOf(Player.Target);
        Player.Target=targets[(index+1)%targets.Count];Player.Lock=0;
    }
    private void SelectInSight()
    {
        ShipUnit? best=null;float dot=.89f;
        foreach(var u in Units)
        {
            if(!u.Alive||u==Player)continue;
            Vector3 delta=u.GlobalPosition-Player.GlobalPosition;
            if(delta.Length()>10000)continue;
            float d=(-Player.GlobalBasis.Z).Dot(delta.Normalized());
            if(d>dot){dot=d;best=u;}
        }
        if(best!=null){Player.Target=best;Player.Lock=0;Sound.Tone(600,.08f,.09f);}
        else Radio("Kein Kontakt im Visier. X / G wechselt feindliche Ziele.");
    }
    private void Command(string command){WingOrder=command;Radio($"Flügel: {command}. Verstanden.");}
    public void Shoot(ShipUnit owner,WeaponSpec weapon,Vector3 origin,Vector3 direction,ShipUnit? target,bool homing)
    {
        if(Shots.Count>350)return;
        var p=new Projectile();actors.AddChild(p);p.GlobalPosition=origin;p.Initialize(this,owner,weapon,direction,target,homing);Shots.Add(p);
    }
    public void Radio(string message){Message=message;RadioTime=6;}
    public void Impact(Vector3 position,string color,float radius)
    {
        var sphere=Visuals.Ball(actors,1,position,color,true);
        sphere.Scale=Vector3.One*Mathf.Max(radius,.5f);
        var tween=CreateTween();tween.TweenProperty(sphere,"scale",Vector3.One*.1f,.22);tween.TweenCallback(Callable.From(sphere.QueueFree));
    }
    public void Explode(Vector3 position,float radius)
    {
        Impact(position,"ffbf79",radius);Sound.Tone(48,1,Mathf.Min(.4f,radius/150),true);
        int count=radius>100?30:10;
        for(int i=0;i<count;i++)
        {
            var dir=new Vector3(Rng.RandfRange(-1,1),Rng.RandfRange(-1,1),Rng.RandfRange(-1,1)).Normalized();
            var debris=Visuals.Box(actors,Vector3.One*Rng.RandfRange(1,4),position,"e7884e",true);
            var tw=CreateTween().SetParallel();tw.TweenProperty(debris,"position",position+dir*radius*4,2.5);tw.TweenProperty(debris,"scale",Vector3.Zero,2.5);
            tw.Chain().TweenCallback(Callable.From(debris.QueueFree));
        }
    }
    public void Finish(bool success,string reason)
    {
        Success=success;ResultText=reason;Phase=GamePhase.Debrief;Paused=Cruise=false;cockpit.Visible=false;Sound.Engine=0;
        if(!outcomeRecorded)
        {
            outcomeRecorded=true;BestKills=Math.Max(BestKills,Kills);
            if(success){Completed++;if(BestTime<=0||MissionTime<BestTime)BestTime=MissionTime;}
            SaveProfile();
        }
        Hud.ShowDebrief();
    }
    public void LoadProfile()
    {
        var cfg=new ConfigFile();if(cfg.Load("user://pilot.cfg")!=Error.Ok)return;
        Difficulty=(int)cfg.GetValue("settings","difficulty",1);
        Difficulty=Math.Clamp(Difficulty,0,2);
        Controls.Sensitivity=(float)cfg.GetValue("settings","sensitivity",1f);
        Controls.InvertPitch=(bool)cfg.GetValue("settings","invert",false);
        Muted=(bool)cfg.GetValue("settings","muted",false);
        Completed=(int)cfg.GetValue("stats","completed",0);BestKills=(int)cfg.GetValue("stats","kills",0);BestTime=(float)cfg.GetValue("stats","time",0f);
    }
    public void SaveProfile()
    {
        if(OS.GetCmdlineUserArgs().Contains("--self-test"))return;
        var cfg=new ConfigFile();cfg.SetValue("settings","difficulty",Difficulty);cfg.SetValue("settings","sensitivity",Controls.Sensitivity);
        cfg.SetValue("settings","invert",Controls.InvertPitch);cfg.SetValue("settings","muted",Muted);
        cfg.SetValue("stats","completed",Completed);cfg.SetValue("stats","kills",BestKills);cfg.SetValue("stats","time",BestTime);
        var error=cfg.Save("user://pilot.cfg");if(error!=Error.Ok)GD.PushWarning($"Pilotprofil konnte nicht gespeichert werden: {error}");
    }
    public override void _ExitTree(){Visuals.ClearCache();Rng.Dispose();}
    private void RunSelfTests()
    {
        try{RunChecks();}catch(Exception ex){GD.PrintErr(ex.ToString());GetTree().Quit(1);}
    }
    private void RunChecks()
    {
        int checks=0;
        void Check(bool condition,string name){if(!condition)throw new Exception("TEST FAILED: "+name);checks++;}
        foreach(var kind in Enum.GetValues<MissionKind>())for(int seed=0;seed<100;seed++)
        {
            var p=MissionPlan.Generate(kind,seed,1);var copy=MissionPlan.Generate(kind,seed,1);
            Check(p.Points.Count>=3&&p.Points.Count<=5,"route length");
            Check(p.ShipId==(kind==MissionKind.Patrol?"medium":kind==MissionKind.Escort?"light":"bomber"),"ship assignment");
            Check(p.Points[0].Encounter==Encounter.Empty,"safe entry");
            Check(kind==MissionKind.Patrol?p.Points[^1].Encounter==Encounter.Squadron:p.Points[^1].Encounter==Encounter.Carrier,"guaranteed objective");
            for(int i=0;i<p.Points.Count;i++)
            {
                Check(p.Points[i].Position==copy.Points[i].Position&&p.Points[i].Encounter==copy.Points[i].Encounter,"determinism");
                if(i>0)Check(p.Points[i].Position.DistanceTo(p.Points[i-1].Position)>2100,"spaced encounters");
            }
        }
        Check(Mathf.IsEqualApprox(GeometryMath.SegmentSphere(new Vector3(0,0,-10),new Vector3(0,0,20),2),.4f),"swept hit");
        Check(GeometryMath.SegmentSphere(new Vector3(10,0,-10),new Vector3(0,0,20),2)<0,"miss");
        Check(GeometryMath.SegmentSphere(Vector3.Zero,Vector3.One,2)==0,"inside hit");
        Prepare(MissionKind.Strike,42);StartLaunch();
        var opponent=Spawn("enemy",false,new Vector3(0,0,-2000));
        float before=opponent.Hull;opponent.TakeDamage(10,1,opponent.GlobalPosition+Vector3.Back,Player);
        Check(opponent.Hull==before&&opponent.RearShield<opponent.Spec.Shield,"rear shield absorbs");
        opponent.TakeDamage(10000,1,Vector3.Zero,Player);Check(!opponent.Alive,"lethal hit");
        Phase=GamePhase.Flight;Player.Model!.Visible=false;cockpit.Visible=true;
        var target=Spawn("carrier",false,Player.GlobalPosition+new Vector3(0,0,-1200));
        Player.Target=target;Player.FireMissile(target,"torpedo");
        Check(Player.Torpedoes==Player.Spec.Torpedoes-1&&Shots.Count==1,"torpedo inventory");
        var projectile=Shots[0];int defence=Plan!.Difficulty;Plan.Difficulty=0;
        for(int i=0;i<600&&projectile.Alive;i++)projectile.Tick(1f/60);
        Plan.Difficulty=defence;
        Check(!projectile.Alive&&target.FrontShield+target.RearShield<target.Spec.Shield*2,"guided torpedo impact");
        Cruise=false;Player.Speed=Player.Spec.Speed*5;Player.Throttle=.4f;
        var travelStart=Player.GlobalPosition;
        for(int i=0;i<120;i++)Player.Tick(1f/60);
        Check(Player.Speed<Player.Spec.Speed&&Player.GlobalPosition.DistanceTo(travelStart)<900,"cruise disengagement brakes before waypoint");
        Player.GlobalPosition=Plan!.Points[0].Position;TickMission(.01f);TickMission(3);
        // A test carrier is still alive, so clear it before checking route advancement.
        target.Alive=false;TickMission(3);Check(NavIndex==1,"empty waypoint advances");
        foreach(var kind in Enum.GetValues<MissionKind>())
        {
            Prepare(kind,731);StartLaunch();Phase=GamePhase.Flight;
            for(int n=0;n<Plan!.Points.Count;n++)
            {
                Player.GlobalPosition=Plan.Points[n].Position;TickMission(.01f);
                foreach(var u in Units.Where(u=>!u.Friendly).ToArray())u.Alive=false;
                TickMission(3);
            }
            Check(Returning,"mission returns after objective");
            Player.GlobalPosition=NavPosition;Player.Speed=0;TickMission(.01f);
            Check(Phase==GamePhase.Landing,"safe docking enters cinematic");
        }
        Prepare(MissionKind.Escort,42);StartLaunch();Phase=GamePhase.Flight;
        foreach(var u in Units.Where(u=>u.Friendly&&u.Spec.Id=="bomber"))u.Alive=false;
        TickMission(.01f);Check(Phase==GamePhase.Debrief&&!Success,"escort failure");
        Check(InputMap.ActionGetEvents("fire").Any(e=>e is InputEventJoypadMotion m&&m.Axis==JoyAxis.TriggerLeft),"controller trigger binding");
        Check(InputMap.ActionGetEvents("warhead").Any(e=>e is InputEventJoypadButton b&&b.ButtonIndex==JoyButton.RightShoulder),"controller shoulder binding");
        GD.Print($"SELF TEST PASS: {checks} checks");GetTree().Quit(0);
    }
    private async void CaptureQa(string mode)
    {
        if(mode!="menu")
        {
            Prepare(MissionKind.Strike,651);
            if(mode!="briefing")
            {
                StartLaunch();
                if(mode=="flight")
                {
                    Phase=GamePhase.Flight;Paused=true;Player.Model!.Visible=false;cockpit.Visible=true;
                    NavIndex=Plan!.Points.Count-1;
                    Player.GlobalPosition=Plan.Points[NavIndex].Position+new Vector3(0,95,350);
                    Activate(Plan.Points[NavIndex]);Player.Target=EnemyCarrier;Player.Lock=3;
                    Camera.GlobalTransform=Player.GlobalTransform;
                }
            }
        }
        for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        string output="user://capture-"+mode+".png";
        foreach(string arg in OS.GetCmdlineUserArgs())if(arg.StartsWith("--capture-output="))output=arg[17..];
        var error=GetViewport().GetTexture().GetImage().SavePng(output);
        GD.Print($"CAPTURE {mode}: {error}");GetTree().Quit(error==Error.Ok?0:1);
    }

}
