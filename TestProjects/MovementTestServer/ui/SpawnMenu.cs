using Godot;
using System;

public partial class SpawnMenu : Control
{

	Server server;
	Button SpawnButton;

	public override void _Ready()
	{
		server = GetNode<Server>("/root/Server");
		SpawnButton = GetNode<Button>("SpawnButton");
		SpawnButton.Connect("pressed", new Callable(this, "SpawnButtonPressed"));
	}

	void SpawnButtonPressed()
	{
		server.RequestSpawn();
	}
}
