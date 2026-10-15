using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SpaceFleet;

public sealed class ShipSpec
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public float Speed { get; set; }
    public float Turn { get; set; }
    public float Hull { get; set; }
    public float Shield { get; set; }
    public float EnergyRegen { get; set; }
    public int Missiles { get; set; }
    public int Torpedoes { get; set; }
    public float Radius { get; set; }
    public string[] Weapons { get; set; } = Array.Empty<string>();
    public string[] MissileTypes { get; set; } = Array.Empty<string>();
}
public sealed class WeaponSpec
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Damage { get; set; }
    public float ShieldMultiplier { get; set; } = 1;
    public float Speed { get; set; }
    public float Cooldown { get; set; }
    public float Cost { get; set; }
    public float Life { get; set; }
    public string Color { get; set; } = "ffffff";
    public float LockTime { get; set; }
    public float Turn { get; set; }
}
public enum MissionKind { Patrol, Escort, Strike }
public enum GamePhase { Menu, Briefing, Launch, Flight, Landing, Debrief }
public enum Encounter { Empty, Squadron, Carrier }
public sealed class NavPoint
{
    public Vector3 Position;
    public Encounter Encounter;
    public bool Activated;
    public bool Cleared;
    public string Name = "";
}
public sealed class MissionPlan
{
    public int Seed;
    public MissionKind Kind;
    public int Difficulty;
    public string ShipId = "";
    public List<NavPoint> Points = new();
    public static MissionPlan Generate(MissionKind kind, int seed, int difficulty)
    {
        var r = new Random(seed);
        var p = new MissionPlan { Kind=kind, Seed=seed, Difficulty=difficulty,
            ShipId=kind==MissionKind.Escort?"light":kind==MissionKind.Strike?"bomber":"medium" };
        int count=r.Next(3,6);
        var pos=new Vector3(0,0,-900);
        for (int i=0;i<count;i++)
        {
            pos+=new Vector3(r.Next(-750,751),r.Next(-300,301),-r.Next(2200,3201));
            var encounter=i==0 ? Encounter.Empty : r.NextDouble()<0.68 ? Encounter.Squadron : Encounter.Empty;
            if (i==count-1 && kind!=MissionKind.Patrol) encounter=Encounter.Carrier;
            if (i==count-1 && kind==MissionKind.Patrol) encounter=Encounter.Squadron;
            p.Points.Add(new NavPoint { Name=$"NAV {i+1:00}", Position=pos, Encounter=encounter });
        }
        return p;
    }
}
public static class Database
{
    public static readonly Dictionary<string,ShipSpec> Ships=new();
    public static readonly Dictionary<string,WeaponSpec> Weapons=new();
    public static void Load()
    {
        Ships.Clear(); Weapons.Clear();
        using var s=FileAccess.Open("res://Data/ships.json", FileAccess.ModeFlags.Read);
        using var w=FileAccess.Open("res://Data/weapons.json", FileAccess.ModeFlags.Read);
        foreach (var item in JsonSerializer.Deserialize<ShipSpec[]>(s.GetAsText())!) Ships[item.Id]=item;
        foreach (var item in JsonSerializer.Deserialize<WeaponSpec[]>(w.GetAsText())!) Weapons[item.Id]=item;
    }
}
