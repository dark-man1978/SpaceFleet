using Godot;
using System;

namespace SpaceFleet;

public partial class ShipUnit : Node3D
{
    public ShipSpec Spec=null!;
    public Game Game=null!;
    public bool Friendly,IsPlayer,Alive=true;
    public ShipUnit? ParentCarrier;
    public Vector3 MountOffset;
    public float Hull,FrontShield,RearShield,Energy=100,Speed,Throttle=.55f,WeaponTimer,MissileTimer,SinceHit=9;
    public float Lock,BoostCharge=100;
    public int WeaponIndex,MissileIndex;
    public int Missiles,Torpedoes;
    public ShipUnit? Target;
    public float AiTime,AiFireTimer;
    public Vector3 Velocity;
    public string Callsign="";
    public Node3D? Model;
    public float Radius=>Spec.Radius;
    public bool IsCarrier=>Spec.Id=="carrier";
    public bool IsGenerator=>Spec.Id=="generator";
    public string WeaponId=>Spec.Weapons.Length>0?Spec.Weapons[WeaponIndex%Spec.Weapons.Length]:"pulse";
    public string MissileId=>Spec.MissileTypes.Length>0?Spec.MissileTypes[MissileIndex%Spec.MissileTypes.Length]:"dart";
    public int Ammo=>MissileId=="torpedo"?Torpedoes:Missiles;
    public float ShieldFraction=>Spec.Shield<=0?0:(FrontShield+RearShield)/(2*Spec.Shield);
    public void Initialize(Game game,string id,bool friendly,bool player=false)
    {
        Game=game;Spec=Database.Ships[id];Friendly=friendly;IsPlayer=player;
        Hull=Spec.Hull;FrontShield=RearShield=Spec.Shield;Missiles=Spec.Missiles;Torpedoes=Spec.Torpedoes;
        Callsign=Spec.Name;
        if(!player)
        {
            if(IsCarrier) Model=Visuals.Carrier(this,!friendly);
            else if(IsGenerator)
            {
                Model=new Node3D();AddChild(Model);
                Visuals.Box(Model,new Vector3(26,12,30),Vector3.Zero,"34313e");
                Visuals.Ball(Model,12,new Vector3(0,12,0),"b5a6ed",true);
            }
            else Model=Visuals.Fighter(this,id,!friendly);
        }
        AiTime=game.Rng.RandfRange(0,10);AiFireTimer=game.Rng.RandfRange(.5f,2);
    }
    public void Tick(float dt)
    {
        if(!Alive)return;
        WeaponTimer=Mathf.Max(0,WeaponTimer-dt);MissileTimer=Mathf.Max(0,MissileTimer-dt);SinceHit+=dt;
        Energy=Mathf.Min(100,Energy+Spec.EnergyRegen*dt);
        bool generatorsDown=IsCarrier&&!Game.Units.Exists(u=>u.Alive&&u.ParentCarrier==this);
        if(SinceHit>4 && !generatorsDown)
        {
            FrontShield=Mathf.Min(Spec.Shield,FrontShield+Spec.Shield*.075f*dt);
            RearShield=Mathf.Min(Spec.Shield,RearShield+Spec.Shield*.075f*dt);
        }
        if(IsGenerator)
        {
            if(ParentCarrier is { Alive:true }) GlobalPosition=ParentCarrier.GlobalPosition+ParentCarrier.GlobalBasis*MountOffset;
            else Die();
            return;
        }
        if(IsPlayer) PlayerTick(dt);else AiTick(dt);
    }
    private void PlayerTick(float dt)
    {
        Throttle=Mathf.Clamp(Throttle+Controls.Throttle*.50f*dt,0,1);
        float healthTurn=Hull/Spec.Hull<.25f?.78f:1;
        var basis=Transform.Basis;
        basis=basis.Rotated(basis.Y,-Controls.Yaw*Spec.Turn*healthTurn*dt);
        basis=basis.Rotated(basis.X,Controls.Pitch*Spec.Turn*healthTurn*dt);
        basis=basis.Rotated(basis.Z,-Controls.Roll*1.6f*dt);
        Basis=basis.Orthonormalized();
        bool boost=Input.IsActionPressed("boost")&&BoostCharge>2&&!Game.Cruise;
        BoostCharge=Mathf.Clamp(BoostCharge+(boost?-32:15)*dt,0,100);
        float desired=Spec.Speed*Throttle*(boost?1.85f:1)*(Game.Cruise?5:1);
        float acceleration=!Game.Cruise&&Speed>Spec.Speed*1.85f?Spec.Speed*7:Spec.Speed*.7f;
        Speed=Mathf.MoveToward(Speed,desired,acceleration*dt);
        Velocity=-GlobalBasis.Z*Speed;GlobalPosition+=Velocity*dt;
        if(Input.IsActionJustPressed("weapon")){WeaponIndex=(WeaponIndex+1)%Spec.Weapons.Length;Game.Sound.Tone(550,.08f,.1f);}
        if(Input.IsActionJustPressed("warhead")){MissileIndex=(MissileIndex+1)%Spec.MissileTypes.Length;Lock=0;}
        var missile=Database.Weapons[MissileId];
        if(Target is { Alive:true,Friendly:false } && InCone(Target,.94f,MissileId=="torpedo"?4300:2600)) Lock=Mathf.Min(missile.LockTime,Lock+dt);
        else Lock=Mathf.Max(0,Lock-dt*2);
        if(Input.IsActionPressed("fire")&&!Game.Cruise) FireEnergy();
        if(Input.IsActionJustPressed("missile")&&!Game.Cruise)
        {
            if(Target is not {Alive:true,Friendly:false})Game.Radio("Waffensystem: Kein feindliches Ziel gewählt.");
            else if(Ammo==0)Game.Radio("Waffensystem: Keine Munition für diesen Raketentyp.");
            else if(Lock<missile.LockTime)Game.Radio("Waffensystem: Zielerfassung halten.");
            else FireMissile(Target,MissileId);
        }
    }
    public bool InCone(ShipUnit target,float cone,float distance)
    {
        Vector3 delta=target.GlobalPosition-GlobalPosition;
        return delta.Length()<distance && -GlobalBasis.Z.Dot(delta.Normalized())>cone;
    }
    private void AiTick(float dt)
    {
        AiTime+=dt;AiFireTimer-=dt;
        if(IsCarrier)
        {
            if(AiFireTimer<=0)
            {
                var t=Game.NearestOpponent(this,2300);
                if(t!=null)
                {
                    Vector3 origin=GlobalPosition+new Vector3(Game.Rng.RandfRange(-90,90),55,Game.Rng.RandfRange(-180,180));
                    var lead=t.GlobalPosition+t.Velocity*.35f;
                    Game.Shoot(this,Database.Weapons["pulse"],origin,(lead-origin).Normalized(),null,false);
                }
                AiFireTimer=.24f/(.8f+Game.Plan!.Difficulty*.2f);
            }
            return;
        }
        if(Target is not {Alive:true}||Target.Friendly==Friendly)Target=null;
        ShipUnit? opponent=Target;
        if(Friendly)
        {
            if(Spec.Id=="bomber") opponent=Game.EnemyCarrier is {Alive:true}?Game.EnemyCarrier:Game.NearestOpponent(this,2000);
            else if(Game.WingOrder=="ANGRIFF" && Game.Player.Target is {Alive:true,Friendly:false})opponent=Game.Player.Target;
            else if(Game.WingOrder=="FORMATION"||Game.WingOrder=="RÜCKKEHR")opponent=null;
            else if(Game.WingOrder=="SCHUTZ")
            {
                ShipUnit protectedShip=Game.Units.Find(u=>u.Alive&&u.Friendly&&!u.IsPlayer&&u.Spec.Id=="bomber")??Game.Player;
                opponent=Game.NearestOpponent(protectedShip,3000);
            }
            else opponent=Game.NearestOpponent(this,2800);
        }
        else
        {
            // Raiders preferentially intercept bombers; remaining ships engage the player or escort.
            opponent=Game.Units.Find(u=>u.Alive&&u.Friendly&&u.Spec.Id=="bomber"&&u.GlobalPosition.DistanceTo(GlobalPosition)<4000);
            opponent??=Game.NearestOpponent(this,6000);
        }
        Target=opponent;
        if(opponent is {Alive:true} && !opponent.IsCarrier && MissileTimer<=0 && Missiles>0 && InCone(opponent,.96f,1700) && GlobalPosition.DistanceTo(opponent.GlobalPosition)>300)
        {
            Lock+=dt;
            if(Lock>1.6f){FireMissile(opponent,"dart");MissileTimer=Friendly?16:20;Lock=0;}
        }
        else Lock=0;
        Vector3 desired;
        float targetSpeed=Spec.Speed*.72f;
        if(opponent!=null)
        {
            var delta=opponent.GlobalPosition-GlobalPosition;float dist=delta.Length();
            // Pass the enemy, then turn back instead of orbiting at zero speed.
            desired=dist<150 && !opponent.IsCarrier ? GlobalPosition-GlobalBasis.Z*400 : opponent.GlobalPosition+opponent.Velocity*.25f;
            if(opponent.IsCarrier)
            {
                float orbit=AiTime*.18f;
                desired=dist<650?opponent.GlobalPosition+new Vector3(Mathf.Sin(orbit)*1700,350,Mathf.Cos(orbit)*1700):opponent.GlobalPosition;
                if(Spec.Id=="bomber" && dist<3600 && dist>500 && Torpedoes>0 && AiFireTimer<=0)
                {FireMissile(opponent,"torpedo");AiFireTimer=10;}
            }
            if(InCone(opponent,.89f,1900)&&AiFireTimer<=0)
            {FireEnergy();AiFireTimer=Friendly?.18f:.28f/(.7f+Game.Plan!.Difficulty*.3f);}
        }
        else
        {
            int slot=Game.Units.IndexOf(this);
            desired=Game.Player.GlobalPosition+Game.Player.GlobalBasis*new Vector3((slot%2==0?1:-1)*(65+slot*15),slot*9,100+slot*35);
            float d=GlobalPosition.DistanceTo(desired);
            targetSpeed=Mathf.Clamp(Game.Player.Speed+(d>150?80:-25),25,Game.Cruise?Game.Player.Spec.Speed*6:Spec.Speed*1.9f);
        }
        var direction=desired-GlobalPosition;
        if(direction.LengthSquared()>1)
        {
            Vector3 dir=direction.Normalized();
            Vector3 up=Mathf.Abs(dir.Dot(Vector3.Up))>.98f?Vector3.Right:Vector3.Up;
            var targetBasis=Basis.LookingAt(dir,up);
            Basis=Basis.Slerp(targetBasis,Mathf.Clamp(Spec.Turn*dt*.65f,0,1)).Orthonormalized();
        }
        float acceleration=Friendly&&(Game.Cruise||Speed>Spec.Speed*1.9f)?Spec.Speed*7:90;
        Speed=Mathf.MoveToward(Speed,targetSpeed,acceleration*dt);Velocity=-GlobalBasis.Z*Speed;
        GlobalPosition+=Velocity*dt;
    }
    public void FireEnergy()
    {
        var weapon=Database.Weapons[WeaponId];
        if(WeaponTimer>0||Energy<weapon.Cost)return;
        Energy-=weapon.Cost;WeaponTimer=weapon.Cooldown;
        Vector3 direction=-GlobalBasis.Z;
        if(Target is {Alive:true} && InCone(Target,.994f,2400))
        {
            float travel=GlobalPosition.DistanceTo(Target.GlobalPosition)/weapon.Speed;
            direction=(Target.GlobalPosition+Target.Velocity*travel-GlobalPosition).Normalized();
        }
        if(!Friendly) direction=(direction+new Vector3(Game.Rng.RandfRange(-.035f,.035f),Game.Rng.RandfRange(-.035f,.035f),0)).Normalized();
        Game.Shoot(this,weapon,GlobalPosition+GlobalBasis*new Vector3(IsPlayer?-2:-3,-1,-Radius-3),direction,null,false);
        if(IsPlayer)Game.Sound.Tone(WeaponId=="plasma"?110:WeaponId=="ion"?310:700,.07f,.13f,true);
    }
    public void FireMissile(ShipUnit target,string id)
    {
        if(MissileTimer>0)return;
        if(id=="torpedo"){if(Torpedoes<=0)return;Torpedoes--;}else{if(Missiles<=0)return;Missiles--;}
        var w=Database.Weapons[id];MissileTimer=w.Cooldown;
        Game.Shoot(this,w,GlobalPosition-GlobalBasis.Z*(Radius+8)-GlobalBasis.Y*3,-GlobalBasis.Z,target,true);
        if(IsPlayer){Game.Sound.Tone(130,.35f,.2f,true);Game.Radio(id=="torpedo"?"Atlas: Torpedo unterwegs.":"Rakete abgefeuert.");Lock=0;}
    }
    public void TakeDamage(float damage,float shieldMultiplier,Vector3 origin,ShipUnit attacker)
    {
        if(!Alive)return;
        SinceHit=0;
        bool front=(-GlobalBasis.Z).Dot((origin-GlobalPosition).Normalized())>=0;
        float shield=front?FrontShield:RearShield;
        float absorbed=Mathf.Min(shield,damage*shieldMultiplier);
        if(front)FrontShield-=absorbed;else RearShield-=absorbed;
        Hull-=Mathf.Max(0,damage-absorbed/shieldMultiplier);
        Game.Impact(GlobalPosition,shield>0?"79c8f5":"ffac63",Mathf.Min(Radius*.6f,12));
        if(IsPlayer){Game.HitFlash=.38f;Game.Sound.Tone(shield>0?230:65,.12f,.2f,true);}
        if(Hull<=0)Die(attacker);
    }
    public void Die(ShipUnit? killer=null)
    {
        if(!Alive)return;
        Alive=false;Visible=false;
        Game.Explode(GlobalPosition,IsCarrier?200:IsGenerator?28:22);
        if(IsGenerator&&ParentCarrier is {Alive:true})
        {
            ParentCarrier.FrontShield=Mathf.Max(0,ParentCarrier.FrontShield-ParentCarrier.Spec.Shield*.7f);
            ParentCarrier.RearShield=Mathf.Max(0,ParentCarrier.RearShield-ParentCarrier.Spec.Shield*.7f);
            Game.Radio("Schildgenerator zerstört. Trägerschilde verlieren Leistung.");
        }
        if(!Friendly&&!IsGenerator && killer is {Friendly:true}){Game.Kills++;if(!IsCarrier)Game.Radio("Feindliches Schiff zerstört.");}
        if(IsCarrier)Game.Radio("Tyrant bricht auseinander! Alle Schiffe auf Abstand.");
        if(Friendly&&!IsPlayer)Game.Radio($"{Callsign}: Schiff verloren.");
    }
}
