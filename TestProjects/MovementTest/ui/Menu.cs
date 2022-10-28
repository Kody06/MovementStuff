using Godot;
using System;

public partial class Menu : Control
{

	Server server;
	Button ConnectToServerButton;

	public override void _Ready()
	{
		server = GetNode<Server>("/root/Server");
		ConnectToServerButton = GetNode<Button>("ConnectToServerButton");
		ConnectToServerButton.Connect("pressed", new Callable(this, "ConnectToServerButtonPressed"));
	}

	void ConnectToServerButtonPressed()
	{
		server.ConnectToServer();
	}
}
