using Godot;
using System;

public partial class DevMap : Node3D
{



	public override void _Ready()
	{
		if(!Multiplayer.IsServer())
		{
			PackedScene SpawnMenuPS = GD.Load<PackedScene>("res://ui/SpawnMenu.tscn");
			Control SpawnMenuScene = (Control)SpawnMenuPS.Instantiate();
			AddChild(SpawnMenuScene);
		}
	}
}
