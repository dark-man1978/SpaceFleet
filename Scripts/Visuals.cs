using Godot;
using System;
using System.Collections.Generic;

namespace SpaceFleet;

public static class Visuals
{
    private static readonly Dictionary<string,StandardMaterial3D> Materials=new();
    public static void ClearCache(){foreach(var m in Materials.Values)m.Dispose();Materials.Clear();}
    public static StandardMaterial3D Material(string color,bool glow=false,float metallic=0.65f)
    {
        string key=color+glow+metallic;
        if(Materials.TryGetValue(key,out var existing)) return existing;
        var c=new Color(color);
        var m=new StandardMaterial3D { AlbedoColor=c, Metallic=metallic, Roughness=0.48f };
        if(glow){m.ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded;m.EmissionEnabled=true;m.Emission=c;m.EmissionEnergyMultiplier=2;}
        Materials[key]=m; return m;
    }
    public static MeshInstance3D Mesh(Node3D parent,Mesh mesh,Vector3 pos,string color,bool glow=false)
    {
        var n=new MeshInstance3D { Mesh=mesh,Position=pos,MaterialOverride=Material(color,glow) };
        parent.AddChild(n);return n;
    }
    public static MeshInstance3D Box(Node3D parent,Vector3 size,Vector3 pos,string color,bool glow=false)
        => Mesh(parent,new BoxMesh { Size=size },pos,color,glow);
    public static MeshInstance3D Ball(Node3D parent,float radius,Vector3 pos,string color,bool glow=false)
        => Mesh(parent,new SphereMesh { Radius=radius,Height=radius*2,RadialSegments=16,Rings=8 },pos,color,glow);
    public static MeshInstance3D Beam(Node3D parent,Vector3 a,Vector3 b,float width,string color,bool glow=false)
    {
        var node=Box(parent,new Vector3(width,width,a.DistanceTo(b)),(a+b)*0.5f,color,glow);
        if(a.DistanceSquaredTo(b)>0.001f) node.LookAtFromPosition((a+b)*0.5f,b,Mathf.Abs((b-a).Normalized().Dot(Vector3.Up))>0.98f?Vector3.Right:Vector3.Up);
        return node;
    }
    // A six-faced tapered hull, built from vertices rather than external model assets.
    public static void Wedge(Node3D parent,Vector3 pos,Vector3 size,string color)
    {
        Vector3[] v={new(-.25f,-.22f,-.5f),new(.25f,-.22f,-.5f),new(.25f,.12f,-.5f),new(-.25f,.12f,-.5f),
            new(-.5f,-.5f,.5f),new(.5f,-.5f,.5f),new(.5f,.5f,.5f),new(-.5f,.5f,.5f)};
        int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4};
        var st=new SurfaceTool(); st.Begin(Godot.Mesh.PrimitiveType.Triangles);
        foreach(int i in faces) st.AddVertex(v[i]*size);
        st.GenerateNormals(); Mesh(parent,st.Commit(),pos,color);
    }
    private static Node3D ImportedModel(Node3D parent,string path,string name)
    {
        var scene=GD.Load<PackedScene>(path);
        if(scene==null)throw new InvalidOperationException($"Fleet model missing: {path}");
        var model=scene.Instantiate<Node3D>();model.Name=name;parent.AddChild(model);return model;
    }
    public static Node3D Fighter(Node3D parent,string type,bool enemy)
    {
        string asset=enemy?"raider":type switch
        {
            "light"=>"kestrel",
            "medium"=>"vanguard",
            "bomber"=>"atlas",
            _=>throw new ArgumentOutOfRangeException(nameof(type),type,"Unknown fighter model")
        };
        return ImportedModel(parent,$"res://Assets/Models/{asset}_fleet_a.glb",char.ToUpperInvariant(asset[0])+asset[1..]+"FleetA");
    }
    public static Node3D Carrier(Node3D parent,bool enemy)
        =>ImportedModel(parent,enemy?"res://Assets/Models/tyrant_fleet_a.glb":"res://Assets/Models/resolute_fleet_a.glb",enemy?"TyrantFleetA":"ResoluteFleetA");
    public static Node3D Cockpit(Node3D player)
        =>ImportedModel(player,"res://Assets/Models/cockpit_fleet_a.glb","FleetCockpit");
    public static void Universe(Node3D root,int seed)
    {
        var env=new WorldEnvironment();root.AddChild(env);
        env.Environment=new Godot.Environment { BackgroundMode=Godot.Environment.BGMode.Color,
            BackgroundColor=new Color("030611"),AmbientLightSource=Godot.Environment.AmbientSource.Color,
            AmbientLightColor=new Color("7f99be"),AmbientLightEnergy=.65f,TonemapMode=Godot.Environment.ToneMapper.Filmic };
        var sun=new DirectionalLight3D { LightColor=new Color("d5e5ff"),LightEnergy=1.6f,RotationDegrees=new Vector3(-32,-35,0),ShadowEnabled=false };root.AddChild(sun);
        var rim=new DirectionalLight3D { LightColor=new Color("b27055"),LightEnergy=.65f,RotationDegrees=new Vector3(40,160,0) };root.AddChild(rim);
        var random=new Random(seed);
        var mm=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
            Mesh=new SphereMesh { Radius=25,Height=50,RadialSegments=4,Rings=3 },InstanceCount=2800 };
        for(int i=0;i<mm.InstanceCount;i++)
        {
            var v=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1).Normalized();
            float scale=(float)random.NextDouble()*1.8f+.4f;
            mm.SetInstanceTransform(i,new Transform3D(Basis.Identity.Scaled(Vector3.One*scale),v*65000));
        }
        root.AddChild(new MultiMeshInstance3D { Multimesh=mm,MaterialOverride=Material("abbcd1",true) });
        var planet=Ball(root,7000,new Vector3(14000,-8500,-33000),"435e76");
        planet.Mesh=new SphereMesh { Radius=7000,Height=14000,RadialSegments=64,Rings=32 };
        // Broad surface bands and atmospheric rim, procedural and lightweight.
        var shader=new Shader { Code=@"shader_type spatial;
render_mode cull_back;
uniform vec4 ocean : source_color = vec4(0.13,0.24,0.32,1.0);
varying vec3 object_position;
void vertex(){object_position=VERTEX;}
void fragment(){
float bands=sin(object_position.y*0.003+sin(object_position.x*0.0018)*3.0);
float land=sin(object_position.x*0.0012)*cos(object_position.z*0.0014)+sin(object_position.y*0.002);
ALBEDO=mix(ocean.rgb,vec3(0.33,0.40,0.36),smoothstep(0.1,0.6,land));
ALBEDO=mix(ALBEDO,vec3(0.68,0.74,0.78),smoothstep(0.82,0.98,bands)*0.65);
ROUGHNESS=1.0;
}" };
        planet.MaterialOverride=new ShaderMaterial { Shader=shader };
        var moon=Ball(root,2100,new Vector3(-19000,11000,-45000),"77777e");
        moon.Mesh=new SphereMesh { Radius=2100,Height=4200,RadialSegments=32,Rings=16 };
    }
}
