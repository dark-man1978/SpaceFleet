using Godot;

namespace SpaceFleet;

public partial class Projectile : Node3D
{
    public Game Game=null!;
    public ShipUnit Shooter=null!;
    public ShipUnit? Target;
    public WeaponSpec Spec=null!;
    public Vector3 Velocity;
    public bool Homing,Alive=true;
    private float age;
    public void Initialize(Game game,ShipUnit owner,WeaponSpec spec,Vector3 dir,ShipUnit? target,bool homing)
    {
        Game=game;Shooter=owner;Spec=spec;Target=target;Homing=homing;Velocity=dir*spec.Speed;
        if(homing)
        {
            Visuals.Wedge(this,Vector3.Zero,new Vector3(1.4f,1.4f,spec.Id=="torpedo"?8:4),"c5ccd0");
            Visuals.Ball(this,.9f,new Vector3(0,0,3),spec.Color,true);
        }
        else Visuals.Box(this,new Vector3(.7f,.7f,11),Vector3.Zero,owner.Friendly?spec.Color:"ff685c",true);
        Orient(dir);
    }
    private void Orient(Vector3 dir)
    {
        if(dir.LengthSquared()>.001f) Basis=Basis.LookingAt(dir,Mathf.Abs(dir.Dot(Vector3.Up))>.98f?Vector3.Right:Vector3.Up);
    }
    public void Tick(float dt)
    {
        if(!Alive)return;
        age+=dt;if(age>Spec.Life){Destroy();return;}
        if(Homing && Target is {Alive:true})
        {
            Vector3 wanted=(Target.GlobalPosition-GlobalPosition).Normalized();
            Vector3 dir=Velocity.Normalized().Lerp(wanted,Mathf.Clamp(Spec.Turn*dt,0,1)).Normalized();
            Velocity=dir*Spec.Speed;Orient(dir);
        }
        Vector3 from=GlobalPosition,to=from+Velocity*dt;
        ShipUnit? nearest=null;float nearestT=float.PositiveInfinity;
        foreach(var u in Game.Units)
        {
            if(!u.Alive||u.Friendly==Shooter.Friendly||u==Shooter)continue;
            float r=u.Radius+(Homing?2:1.2f);
            // Relative swept collision catches fast targets moving across the shot during this tick.
            Vector3 start=from-(u.GlobalPosition-u.Velocity*dt);
            Vector3 relative=(to-from)-u.Velocity*dt;
            float t=GeometryMath.SegmentSphere(start,relative,r);
            if(t>=0 && t<nearestT){nearest=u;nearestT=t;}
        }
        if(nearest!=null)
        {
            GlobalPosition=from.Lerp(to,nearestT);
            nearest.TakeDamage(Spec.Damage,Spec.ShieldMultiplier,from,Shooter);Destroy();return;
        }
        GlobalPosition=to;
        // Capital point defence can shoot down incoming torpedoes. Escorts can remove the carrier first.
        if(Homing&&Spec.Id=="torpedo"&&Target is {Alive:true,IsCarrier:true}&&age>1)
        {
            float distance=GlobalPosition.DistanceTo(Target.GlobalPosition);
            if(distance<900&&Game.Rng.Randf()<dt*.055f*Game.Plan!.Difficulty)
            {Game.Impact(GlobalPosition,"ffba75",8);Destroy();}
        }
    }
    public void Destroy(){Alive=false;QueueFree();}
}
public static class GeometryMath
{
    public static float SegmentSphere(Vector3 start,Vector3 delta,float radius)
    {
        float c=start.LengthSquared()-radius*radius;
        if(c<=0)return 0;
        float a=delta.LengthSquared();if(a<.00001f)return -1;
        float b=start.Dot(delta),d=b*b-a*c;
        if(d<0)return -1;
        float t=(-b-Mathf.Sqrt(d))/a;
        return t>=0&&t<=1?t:-1;
    }
}
