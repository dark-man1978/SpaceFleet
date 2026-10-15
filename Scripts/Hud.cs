using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceFleet;

public partial class Hud : Control
{
    public Game Game=null!;
    private Control? menu;
    private readonly Font font=ThemeDB.FallbackFont;
    private readonly Color ink=new("d4e8eb"), cyan=new("78d9e4"), dim=new("819ba9"), red=new("ff806d"), gold=new("f2c184"), green=new("9ad8b5");
    private string screen="menu";
    public override void _Ready(){SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Ignore;}
    public void ClearMenu()
    {
        menu?.QueueFree();menu=null;screen="";
    }
    private VBoxContainer MenuBox(float x,float y,float width)
    {
        ClearMenu();menu=new Control();AddChild(menu);
        var box=new VBoxContainer {Position=new Vector2(x,y),Size=new Vector2(width,0)};
        box.AddThemeConstantOverride("separation",9);menu.AddChild(box);return box;
    }
    private Button Button(VBoxContainer box,string text,Action action,bool focus=false)
    {
        var b=new Button { Text=text,CustomMinimumSize=new Vector2(0,42),FocusMode=FocusModeEnum.All,Alignment=HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size",19);b.AddThemeColorOverride("font_color",ink);
        b.AddThemeColorOverride("font_focus_color",new Color("07131d"));
        b.AddThemeColorOverride("font_hover_color",new Color("07131d"));
        b.AddThemeColorOverride("font_pressed_color",new Color("07131d"));
        var normal=new StyleBoxFlat {BgColor=new Color(.04f,.09f,.13f,.92f),BorderColor=new Color("34515e"),BorderWidthLeft=2,ContentMarginLeft=16,ContentMarginRight=16};
        b.AddThemeStyleboxOverride("normal",normal);
        var active=(StyleBoxFlat)normal.Duplicate();active.BgColor=cyan;active.BorderColor=cyan;
        b.AddThemeStyleboxOverride("focus",active);b.AddThemeStyleboxOverride("hover",active);b.AddThemeStyleboxOverride("pressed",active);
        b.Pressed+=()=>{Game.Sound.Tone(500,.07f,.09f);action();};box.AddChild(b);
        if(focus)b.CallDeferred(Control.MethodName.GrabFocus);
        return b;
    }
    public void ShowMenu()
    {
        var box=MenuBox(70,245,440);screen="menu";
        Button(box,"01   PATROUILLE  /  Vanguard",()=>Game.Prepare(MissionKind.Patrol),true);
        Button(box,"02   BOMBERESKORTE  /  Kestrel",()=>Game.Prepare(MissionKind.Escort));
        Button(box,"03   TRÄGERANGRIFF  /  Atlas",()=>Game.Prepare(MissionKind.Strike));
        Button(box,"Optionen & Steuerung",ShowSettings);
        Button(box,"Spiel beenden",()=>Game.GetTree().Quit());
    }
    public void ShowSettings()
    {
        var box=MenuBox(70,225,500);screen="settings";
        Button(box,$"Schwierigkeit: {DifficultyName()}",()=>{Game.Difficulty=(Game.Difficulty+1)%3;Game.SaveProfile();ShowSettings();},true);
        Button(box,$"Stickempfindlichkeit: {Controls.Sensitivity:0.0}",()=>{Controls.Sensitivity=Controls.Sensitivity>=1.4f?.6f:Controls.Sensitivity+.2f;Game.SaveProfile();ShowSettings();});
        Button(box,$"Höhensteuerung invertiert: {(Controls.InvertPitch?"Ja":"Nein")}",()=>{Controls.InvertPitch=!Controls.InvertPitch;Game.SaveProfile();ShowSettings();});
        Button(box,$"Ton: {(Game.Muted?"Aus":"Ein")}",()=>{Game.Muted=!Game.Muted;Game.Sound.Muted=Game.Muted;Game.SaveProfile();ShowSettings();});
        Button(box,"Zurück",ShowMenu);
    }
    public void ShowBriefing()
    {
        var box=MenuBox(70,605,440);screen="briefing";
        Button(box,"STARTFREIGABE ANFORDERN",Game.StartLaunch,true);
        Button(box,"Neue Zufallsmission",()=>Game.Prepare(Game.Plan!.Kind));
        Button(box,"Zurück",Game.BackToMenu);
    }
    public void ShowPause()
    {
        var box=MenuBox(440,310,400);screen="pause";
        Button(box,"Flug fortsetzen",()=>{Game.Paused=false;ClearMenu();},true);
        Button(box,"Einsatz abbrechen",()=>Game.Finish(false,"Einsatz vom Piloten abgebrochen."));
        Button(box,"Zum Hauptmenü",Game.BackToMenu);
    }
    public void ShowDebrief()
    {
        var box=MenuBox(70,550,460);screen="debrief";
        Button(box,"Neue Mission gleichen Typs",()=>Game.Prepare(Game.Plan!.Kind),true);
        Button(box,"Diesen Einsatz wiederholen",()=>{Game.Prepare(Game.Plan!.Kind,Game.Plan.Seed);Game.StartLaunch();});
        Button(box,"Zum Hauptmenü",Game.BackToMenu);
    }
    private static string Clock(float seconds)=>TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
    private string DifficultyName()=>Game.Difficulty==0?"Kadett":Game.Difficulty==2?"Veteran":"Pilot";
    private static string MissionName(MissionKind kind)=>kind==MissionKind.Escort?"BOMBERESKORTE":kind==MissionKind.Strike?"TRÄGERANGRIFF":"PATROUILLE";
    private void Text(float x,float y,string text,int size=18,Color? color=null)
        =>DrawString(font,new Vector2(x,y),text,HorizontalAlignment.Left,-1,size,color??ink);
    private void Line(Vector2 a,Vector2 b,Color c,float width=1)=>DrawLine(a,b,c,width,true);
    private void Panel(float x,float y,float w,float h,string title)
    {
        DrawRect(new Rect2(x,y,w,h),new Color(.02f,.045f,.075f,.88f));
        DrawRect(new Rect2(x,y,w,h),new Color(.20f,.35f,.40f,.85f),false,1);
        DrawRect(new Rect2(x,y,3,20),cyan);Text(x+13,y+22,title,15,dim);
    }
    private void Bar(float x,float y,float w,float fraction,Color color,string label)
    {
        Text(x,y,label,15,dim);
        DrawRect(new Rect2(x,y+7,w,7),new Color(.10f,.17f,.21f));
        DrawRect(new Rect2(x,y+7,w*Mathf.Clamp(fraction,0,1),7),color);
    }
    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero,0,Size/new Vector2(1280,800));
        if(Game.Phase==GamePhase.Flight||Game.Phase==GamePhase.Launch||Game.Phase==GamePhase.Landing)DrawFlight();
        if(screen!="")DrawMenu();
    }
    private void DrawMenu()
    {
        if(screen=="pause")
        {
            DrawRect(new Rect2(0,0,1280,800),new Color(0,.015f,.025f,.8f));
            Text(440,255,"EINSATZ PAUSIERT",31);return;
        }
        DrawRect(new Rect2(0,0,600,800),new Color(.014f,.03f,.05f,.94f));
        DrawRect(new Rect2(600,0,680,800),new Color(.01f,.02f,.04f,.14f));
        Text(70,67,"SFS RESOLUTE  /  FLUGDECK 07",16,cyan);
        Text(70,140,"SPACE FLEET",56);
        Text(73,178,"GRENZSEKTOR  /  2186",18,dim);
        Text(70,765,"STEAM DECK  •  COCKPIT COMBAT  •  PROTOTYP 0.1",14,dim);
        if(screen=="menu")
        {
            Text(70,220,"WÄHLE DEINEN EINSATZ",16,gold);
            Text(70,540,"Jeder Einsatz. Eine neue Flugroute.",19);
            Text(70,570,"Schiff und Bewaffnung werden dir zugeteilt.",17,dim);
            Text(70,645,$"Erfolgreiche Einsätze   {Game.Completed:00}",17,cyan);
            Text(70,675,$"Höchste Abschusszahl   {Game.BestKills:00}",17,dim);
            Text(800,620,"SFS RESOLUTE",25);Text(800,650,"Flottenträger • Einsatzgruppe Spectre",17,dim);
            Text(800,710,"D-Pad / Stick: Auswahl     A: Bestätigen",17,cyan);
        }
        if(screen=="settings")
        {
            Text(70,215,"FLUGEINSTELLUNGEN",17,gold);
            Panel(650,205,560,490,"CONTROLLER / TASTATUR");
            string[] rows={"Linker Stick       Drehen / Neigen       WASD","Rechter Stick      Schub / Rollen       R F / Q E","LT / RT             Laser / Rakete       Leertaste / Enter","LB / RB             Waffen wechseln     Tab / C","A / X                Ziel wählen / nächster Feind   T / G","Y                     Navigationspunkt    N","B halten            Nachbrenner          Shift","R3                    Reiseflug ×5            J","D-Pad oben        Formation               1","D-Pad rechts     Mein Ziel angreifen     2","D-Pad unten      Bomber schützen        3","D-Pad links        Rückkehr zu mir         4","Menü                 Pause                     Esc"};
            for(int i=0;i<rows.Length;i++)Text(670,252+i*32,rows[i],16,i<8?ink:dim);
            Text(70,530,"LT halten: Energiewaffen haben unbegrenzte",17,dim);Text(70,557,"Munition. Den Energiespeicher im Blick behalten.",17,dim);
            Text(70,607,"Raketenziel im Visier halten, bis LOCK erscheint.",17,cyan);
        }
        if(screen=="briefing")DrawBriefing();
        if(screen=="debrief")
        {
            Text(70,240,Game.Success?"MISSION ERFOLGREICH":"MISSION GESCHEITERT",27,Game.Success?green:red);
            // Keep the report text readable on the small display.
            string reason=Game.ResultText;int split=reason.LastIndexOf(' ',Math.Min(49,reason.Length-1));
            if(reason.Length>52&&split>0){Text(70,286,reason[..split],18);Text(70,314,reason[(split+1)..],18);}else Text(70,286,reason,18);
            Text(70,375,$"Einsatzzeit          {Clock(Game.MissionTime)}",21);
            Text(70,415,$"Flottenabschüsse    {Game.Kills}",21);
            Text(70,455,$"Überlebende Flügel   {Game.Units.Count(u=>u.Alive&&u.Friendly&&!u.IsPlayer)} / 2",21);
            Text(70,495,$"Seed  {Game.Plan!.Seed}",16,dim);
        }
    }
    private void DrawBriefing()
    {
        var p=Game.Plan!;var ship=Database.Ships[p.ShipId];
        Text(70,235,MissionName(p.Kind),28,gold);
        Text(70,280,ship.Name,26);Text(70,308,ship.Role,18,dim);
        string[] description=p.Kind switch
        {
            MissionKind.Escort=>new[]{"Begleite Atlas 2 und Atlas 3 zum Feindträger.","Halte die Abfangjäger von den Bombern fern.","Mindestens ein Bomber muss überleben."},
            MissionKind.Strike=>new[]{"Führe einen Torpedoangriff auf die Tyrant aus.","Deine Jäger sichern den Anflug.","Zerstöre den Träger und kehre zurück."},
            _=>new[]{"Überprüfe alle zugewiesenen Navigationspunkte.","Bekämpfe feindliche Kräfte im Einsatzgebiet.","Kehre anschließend zur Resolute zurück."}
        };
        for(int i=0;i<description.Length;i++)Text(70,355+i*29,description[i],18);
        Text(70,470,$"Hülle {ship.Hull:0}  /  Schilde {ship.Shield:0} je Sektor",17,cyan);
        Text(70,502,$"Raketen {ship.Missiles}  /  Torpedos {ship.Torpedoes}",17,dim);
        Text(70,548,$"{p.Points.Count} Wegpunkte  •  {DifficultyName()}  •  Seed {p.Seed}",16,dim);
        Panel(650,235,560,360,"TAKTISCHE FLUGROUTE / KONTAKTE UNBEKANNT");
        Vector2 prev=new(705,535);DrawCircle(prev,6,cyan);Text(685,566,"SFS RESOLUTE",14,dim);
        for(int i=0;i<p.Points.Count;i++)
        {
            float x=730+i*400f/Math.Max(1,p.Points.Count-1);
            float y=460-i*130f/Math.Max(1,p.Points.Count-1)+p.Points[i].Position.X*.025f;
            Vector2 point=new(x,y);Line(prev,point,new Color("557d8d"),2);DrawCircle(point,7,i==p.Points.Count-1?gold:cyan);
            Text(x-20,y-19,$"{i+1:00}",17);prev=point;
        }
        Text(674,630,"R3 / J: Reiseflug zwischen gesicherten Punkten.",18,cyan);
        Text(674,665,"Beim Rückflug Schub unter 70 % reduzieren.",17,dim);
    }
    private void DrawFlight()
    {
        if(Game.Phase==GamePhase.Launch||Game.Phase==GamePhase.Landing)
        {
            DrawRect(new Rect2(0,0,1280,80),new Color("030710"));DrawRect(new Rect2(0,705,1280,95),new Color("030710"));
            Text(65,52,Game.Phase==GamePhase.Launch?"SPECTRE 1  /  STARTSEQUENZ":"SFS RESOLUTE  /  LANDEANFLUG",22);
            Text(900,52,"A / LEERTASTE  ÜBERSPRINGEN",16,dim);
            if(Game.RadioTime>0)Text(65,752,Game.Message,18,cyan);return;
        }
        var p=Game.Player;if(p==null)return;
        Vector2 center=new(640,400);
        // Combat crosshair, deliberately clear in its centre.
        Line(center+new Vector2(-24,0),center+new Vector2(-9,0),cyan);
        Line(center+new Vector2(9,0),center+new Vector2(24,0),cyan);
        Line(center+new Vector2(0,-24),center+new Vector2(0,-9),cyan);
        Line(center+new Vector2(0,9),center+new Vector2(0,24),cyan);DrawCircle(center,2,cyan);
        Text(35,39,"SPECTRE 1",20);Text(35,65,p.Spec.Name,15,dim);
        Text(1030,38,$"{MissionName(Game.Plan!.Kind)}",17,cyan);
        Text(1050,65,$"{Clock(Game.MissionTime)}  /  {Game.Kills:00} KILLS",16,dim);
        string objective=Game.Returning?"RÜCKKEHR ZUR RESOLUTE":$"{Game.Plan.Points[Math.Min(Game.NavIndex,Game.Plan.Points.Count-1)].Name}  /  {(Game.Plan.Points[Math.Min(Game.NavIndex,Game.Plan.Points.Count-1)].Activated?"GEBIET SICHERN":"ANFLUG")}";
        Text(430,40,objective,18,gold);Text(500,66,$"ENTFERNUNG  {Game.NavDistance/1000:0.0} km",16,dim);
        DrawMarker(Game.NavPosition,gold,Game.Returning?"RESOLUTE":$"NAV {Game.NavIndex+1:00}",false);
        foreach(var unit in Game.Units)
        {
            if(!unit.Alive||unit==p||unit.GlobalPosition.DistanceTo(p.GlobalPosition)>8500)continue;
            if(unit==p.Target)continue;
            if(!Game.Camera.IsPositionBehind(unit.GlobalPosition))
            {
                var pos=Game.Camera.UnprojectPosition(unit.GlobalPosition);
                if(pos.X>15&&pos.X<1265&&pos.Y>95&&pos.Y<585)
                {
                    Color c=unit.Friendly?green:red;
                    DrawPolyline(new[]{pos+new Vector2(0,-6),pos+new Vector2(6,0),pos+new Vector2(0,6),pos+new Vector2(-6,0),pos+new Vector2(0,-6)},c,1,true);
                }
            }
        }
        if(p.Target is {Alive:true} t)
        {
            DrawMarker(t.GlobalPosition,t.Friendly?green:red,t.Callsign,true);
            if(!Game.Camera.IsPositionBehind(t.GlobalPosition))
            {
                float travel=p.GlobalPosition.DistanceTo(t.GlobalPosition)/Database.Weapons[p.WeaponId].Speed;
                var lead=Game.Camera.UnprojectPosition(t.GlobalPosition+t.Velocity*travel);
                if(lead.Y<590&&lead.Y>90&&lead.X>20&&lead.X<1260){DrawArc(lead,8,0,Mathf.Tau,24,gold,1,true);Line(lead-new Vector2(4,0),lead+new Vector2(4,0),gold);}
            }
        }
        Panel(28,600,290,178,"SCHIFFSZUSTAND");
        Bar(43,650,258,p.FrontShield/p.Spec.Shield,cyan,$"SCHILD VORN   {p.FrontShield/p.Spec.Shield*100:0}%");
        Bar(43,688,258,p.RearShield/p.Spec.Shield,cyan,$"SCHILD HINTEN  {p.RearShield/p.Spec.Shield*100:0}%");
        Bar(43,726,258,p.Hull/p.Spec.Hull,p.Hull/p.Spec.Hull<.3f?red:green,$"HÜLLE          {p.Hull/p.Spec.Hull*100:0}%");
        Panel(333,600,240,178,"WAFFENSYSTEME");
        Text(348,650,Database.Weapons[p.WeaponId].Name.ToUpperInvariant(),19,cyan);
        Bar(348,680,208,p.Energy/100,p.Energy<15?red:cyan,$"ENERGIE   {p.Energy:0}%");
        Text(348,729,$"{Database.Weapons[p.MissileId].Name} ×{p.Ammo}",18,gold);
        float required=Database.Weapons[p.MissileId].LockTime;
        Text(348,758,p.Lock>=required?"LOCK  /  RT FEUERN":p.Lock>0?$"ERFASSUNG  {p.Lock/required*100:0}%":"KEINE ZIELERFASSUNG",14,p.Lock>=required?green:dim);
        DrawRadar();
        Panel(820,600,432,178,"ZIELCOMPUTER");
        if(p.Target is {Alive:true} target)
        {
            Text(836,650,target.Callsign,21,target.Friendly?green:red);
            Text(836,677,$"{target.Spec.Role}  /  {p.GlobalPosition.DistanceTo(target.GlobalPosition)/1000:0.00} km",16,dim);
            Bar(836,706,196,target.ShieldFraction,cyan,$"SCHILD  {target.ShieldFraction*100:0}%");
            Bar(1044,706,191,target.Hull/target.Spec.Hull,gold,$"HÜLLE  {Mathf.Max(0,target.Hull/target.Spec.Hull)*100:0}%");
            Text(836,758,target.Friendly?"VERBÜNDETER KONTAKT":"FEINDLICHER KONTAKT",14,target.Friendly?green:red);
        }
        else {Text(836,655,"KEIN ZIEL AUSGEWÄHLT",21,dim);Text(836,694,"A: Im Visier    X: Nächster Feind",17,cyan);}
        Text(35,565,$"SCHUB {p.Throttle*100:0}%   {p.Speed:0} m/s",18);
        Text(35,588,$"NACHBRENNER {p.BoostCharge:0}%",13,dim);
        Text(925,562,$"FLÜGEL: {Game.WingOrder}",16,dim);
        if(Game.Cruise)Text(500,110,"REISEFLUG ×5  /  R3 BEENDEN",20,gold);
        else if(Game.NearestOpponent(p,4200)==null)Text(450,111,"R3: REISEFLUG    /    Y: NAVIGATION",15,dim);
        if(Game.Shots.Exists(s=>s.Alive&&s.Homing&&s.Target==p))Text(490,160,"WARNUNG: RAKETENANFLUG",24,red);
        if(p.Hull<p.Spec.Hull*.25f)Text(492,195,"HÜLLE KRITISCH",22,red);
        if(Game.RadioTime>0)
        {
            DrawRect(new Rect2(225,510,830,36),new Color(.02f,.05f,.08f,.88f));
            int size=Game.Message.Length>85?14:16;Text(240,534,Game.Message,size,cyan);
        }
        if(Game.HitFlash>0)
        {
            Color c=new(.9f,.2f,.12f,Game.HitFlash*.30f);
            DrawRect(new Rect2(0,0,1280,10),c);DrawRect(new Rect2(0,0,10,800),c);DrawRect(new Rect2(1270,0,10,800),c);DrawRect(new Rect2(0,790,1280,10),c);
        }
    }
    private void DrawRadar()
    {
        Panel(588,600,217,178,"RADAR / 5 km");
        Vector2 c=new(696,699);const float radius=66;
        DrawArc(c,radius,0,Mathf.Tau,64,new Color("395d70"),1,true);DrawArc(c,33,0,Mathf.Tau,48,new Color("284553"),1,true);
        Line(c-new Vector2(radius,0),c+new Vector2(radius,0),new Color("284553"));Line(c-new Vector2(0,radius),c+new Vector2(0,radius),new Color("284553"));
        DrawCircle(c,3,ink);
        foreach(var u in Game.Units)
        {
            if(!u.Alive||u==Game.Player)continue;
            Vector3 local=Game.Player.GlobalBasis.Inverse()*(u.GlobalPosition-Game.Player.GlobalPosition);
            if(local.Length()>5000)continue;
            Vector2 pos=c+new Vector2(local.X,local.Z)/5000*radius;
            Color col=u.Friendly?green:red;
            if(u==Game.Player.Target)DrawArc(pos,6,0,Mathf.Tau,12,gold,1,true);
            DrawCircle(pos,u.IsCarrier?4:2.7f,col);
            Line(pos,pos+new Vector2(0,-Mathf.Clamp(local.Y/5000*radius,-15,15)),col);
        }
        Vector3 n=Game.Player.GlobalBasis.Inverse()*(Game.NavPosition-Game.Player.GlobalPosition);
        Vector2 nv=new(n.X,n.Z);if(nv.Length()>5000)nv=nv.Normalized()*5000;
        DrawCircle(c+nv/5000*radius,2.5f,gold);
    }
    private void DrawMarker(Vector3 world,Color color,string label,bool target)
    {
        bool behind=Game.Camera.IsPositionBehind(world);
        Vector2 pos=behind?new Vector2(-100,-100):Game.Camera.UnprojectPosition(world);
        bool visible=!behind&&pos.X>45&&pos.X<1235&&pos.Y>140&&pos.Y<485;
        if(visible)
        {
            float s=target?30:15;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {
                Vector2 corner=pos+new Vector2(x*s,y*s);
                Line(corner,corner-new Vector2(x*9,0),color,2);Line(corner,corner-new Vector2(0,y*9),color,2);
            }
            Text(pos.X-s,pos.Y-s-12,label,15,color);
        }
        else
        {
            Vector3 local=Game.Camera.GlobalBasis.Inverse()*(world-Game.Camera.GlobalPosition);
            var direction=new Vector2(local.X,-local.Y);
            if(direction.LengthSquared()<.01f)direction=new Vector2(0,1);
            direction=direction.Normalized();
            float factor=Mathf.Min(560/Mathf.Max(.001f,Mathf.Abs(direction.X)),185/Mathf.Max(.001f,Mathf.Abs(direction.Y)));
            var p=new Vector2(640,330)+direction*factor;Vector2 side=new(-direction.Y,direction.X);
            DrawColoredPolygon(new[]{p+direction*10,p-direction*5+side*6,p-direction*5-side*6},color);
            Text(Mathf.Clamp(p.X-45,25,1110),p.Y+28,behind?label+" ↶":label,13,color);
        }
    }
}
