using Godot;

namespace SpaceFleet;

public static class Controls
{
    public static float Sensitivity=1;
    public static bool InvertPitch=false;
    public static void Install()
    {
        Bind("fire",Key.Space,JoyButton.Invalid,JoyAxis.TriggerLeft);
        Bind("missile",Key.Enter,JoyButton.Invalid,JoyAxis.TriggerRight);
        Bind("weapon",Key.Tab,JoyButton.LeftShoulder);
        Bind("warhead",Key.C,JoyButton.RightShoulder);
        Bind("target",Key.T,JoyButton.A);
        Bind("enemy",Key.G,JoyButton.X);
        Bind("nav",Key.N,JoyButton.Y);
        Bind("boost",Key.Shift,JoyButton.B);
        Bind("pause",Key.Escape,JoyButton.Start);
        Bind("cruise",Key.J,JoyButton.RightStick);
        Bind("skip",Key.Space,JoyButton.A);
        Bind("command_form",Key.Key1,JoyButton.DpadUp);
        Bind("command_attack",Key.Key2,JoyButton.DpadRight);
        Bind("command_protect",Key.Key3,JoyButton.DpadDown);
        Bind("command_return",Key.Key4,JoyButton.DpadLeft);
        Bind("yaw_left",Key.A); Bind("yaw_right",Key.D);
        Bind("pitch_up",Key.W); Bind("pitch_down",Key.S);
        Bind("roll_left",Key.Q); Bind("roll_right",Key.E);
        Bind("throttle_up",Key.R); Bind("throttle_down",Key.F);
        if (!InputMap.HasAction("ui_accept")) InputMap.AddAction("ui_accept");
    }
    private static void Bind(string name,Key key,JoyButton button=JoyButton.Invalid,JoyAxis axis=JoyAxis.Invalid)
    {
        if(InputMap.HasAction(name)) InputMap.EraseAction(name);
        InputMap.AddAction(name,0.15f);
        InputMap.ActionAddEvent(name,new InputEventKey { PhysicalKeycode=key });
        if(button!=JoyButton.Invalid) InputMap.ActionAddEvent(name,new InputEventJoypadButton { ButtonIndex=button,Device=-1 });
        if(axis!=JoyAxis.Invalid) InputMap.ActionAddEvent(name,new InputEventJoypadMotion { Axis=axis,AxisValue=1,Device=-1 });
    }
    public static float Axis(JoyAxis axis)
    {
        var pads=Input.GetConnectedJoypads();
        if(pads.Count==0) return 0;
        float v=Input.GetJoyAxis(pads[0],axis);
        return Mathf.Abs(v)<0.16f?0:Mathf.Sign(v)*(Mathf.Abs(v)-0.16f)/0.84f;
    }
    public static float Yaw => Mathf.Clamp(Input.GetAxis("yaw_left","yaw_right")+Axis(JoyAxis.LeftX),-1,1)*Sensitivity;
    public static float Pitch => Mathf.Clamp(Input.GetAxis("pitch_down","pitch_up")-Axis(JoyAxis.LeftY),-1,1)*Sensitivity*(InvertPitch?-1:1);
    public static float Roll => Mathf.Clamp(Input.GetAxis("roll_left","roll_right")+Axis(JoyAxis.RightX),-1,1)*Sensitivity;
    public static float Throttle => Mathf.Clamp(Input.GetAxis("throttle_down","throttle_up")-Axis(JoyAxis.RightY),-1,1);
}
