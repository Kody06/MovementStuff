using Godot;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public partial class Server : Node
{
	Timer ClockTimer;
	public int Clock;
	int Latency;
	List<int> LatencyList;
	int DeltaLatency;

	ENetMultiplayerPeer ServerNetwork = new ENetMultiplayerPeer();
	int ServerPort = 6001;
	int MaxPlayers = 40;

	string Map = "DevMap";

	ENetMultiplayerPeer ClientNetwork = new ENetMultiplayerPeer();
	string IP = "127.0.0.1";
	int Port = 6001;

	public override void _Ready()
	{
		Clock = 0;
		Timer CT = new Timer();
		CT.Name = "ClockTimer";
		CT.Autostart = false;
		CT.OneShot = false;
		CT.WaitTime = 0.001f;
		AddChild(CT);
		ClockTimer = GetNode<Timer>("ClockTimer");
		ClockTimer.Connect("timeout", new Callable(this, "ClockTimerTimeout"));
		StartServer();
	}

	void ClockTimerTimeout()
	{
		if(Multiplayer.IsServer())
		{
			Clock += 1;
		}
		else
		{
			Clock += 1 + DeltaLatency;
			DeltaLatency = 0;
		}
	}

	void StartServer()
	{
		ServerNetwork.CreateServer(ServerPort, MaxPlayers);
		Multiplayer.MultiplayerPeer = ServerNetwork;
		GD.Print("Server Started");

		ServerNetwork.Connect("peer_connected", new Callable(this, "PeerConnected"));
		ServerNetwork.Connect("peer_disconnected", new Callable(this, "PeerDisconnected"));

		CallDeferred("ServerLoadMap");

		ClockTimer.Start();
	}

	void ServerLoadMap()
	{
		PackedScene MapPS = GD.Load<PackedScene>("res://maps/" + Map + ".tscn");
		Node3D MapScene = (Node3D)MapPS.Instantiate();
		GetTree().Root.AddChild(MapScene);
	}

	void PeerConnected(int PlayerID)
	{
		GD.Print("Player " + PlayerID + " Connected");
		System.Timers.Timer PlayerConnectTimer = new System.Timers.Timer(2000);
		PlayerConnectTimer.Elapsed += (s, e) => PlayerConnectTimerTimeout(s, e, PlayerID);
		PlayerConnectTimer.AutoReset = false;
		PlayerConnectTimer.Enabled = true;
	}

	[RPC(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void PlayerConnectTimerTimeout(object s, System.Timers.ElapsedEventArgs e, int PlayerID)
	{
		RpcId(PlayerID, "RpcLoadMap", Map);
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer)]
	public void RpcLoadMap(string MapToLoad)
	{
		GetTree().ChangeSceneToFile("res://maps/" + Map + ".tscn");
	}

	void PeerDisconnected(int PlayerID)
	{
		GD.Print("Player " + PlayerID + " Disconnected");
	}

	public void ConnectToServer()
	{
		LatencyList = new List<int>();
		ClientNetwork.CreateClient(IP, Port);
		Multiplayer.MultiplayerPeer = ClientNetwork;

		ClientNetwork.Connect("connection_succeeded", new Callable(this, "ConnectionSucceeded"));
		ClientNetwork.Connect("connection_failed", new Callable(this, "ConnectionFailed"));
		ClockTimer.Start();
	}

	[RPC(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ConnectionSucceeded()
	{
		RpcId(1, "GetServerTime", Clock);
		Timer DLT = new Timer();
		DLT.Name = "DeltaLatancyTimer";
		DLT.Autostart = true;
		DLT.OneShot = false;
		DLT.WaitTime = 0.5f;
		AddChild(DLT);
		Timer DeltaLatancyTimer = GetNode<Timer>("DeltaLatancyTimer");
		DeltaLatancyTimer.Connect("timeout", new Callable(this, "GetLatency"));
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void GetServerTime(int ClientTime)
	{
		int PlayerID = Multiplayer.GetRemoteSenderId();
		RpcId(PlayerID, "ReveiveServerTime", Clock, ClientTime);
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer)]
	public void ReveiveServerTime(int ServerTime, int ClientTime)
	{
		Latency = (Clock - ClientTime) / 2;
		Clock = ServerTime + Latency;
	}

	[RPC(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void GetLatency()
	{
		RpcId(1, "RPCGetLatency", Clock);
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RPCGetLatency(int ClientTime)
	{
		int PlayerID = Multiplayer.GetRemoteSenderId();
		RpcId(PlayerID, "ReveiveLatency", ClientTime);
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer)]
	public void ReveiveLatency(int ClientTime)
	{
		LatencyList.Add((Clock - ClientTime) / 2);
		if(LatencyList.Count == 9)
		{
			var TotalLatency = 0;
			LatencyList.Sort();
			var MidPoint = LatencyList[4];
			for(int i = 9; i < -1; i--)
			{
				if(LatencyList[i] > (MidPoint * 2) && LatencyList[i] > 20)
				{
					LatencyList.RemoveAt(i);
				}
				else
				{
					TotalLatency += LatencyList[i];
				}
			}
			DeltaLatency = (TotalLatency / LatencyList.Count) - Latency;
			Latency = TotalLatency / LatencyList.Count;
			LatencyList.Clear();
		}
	}

	void ConnectionFailed()
	{

	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RequestSpawn()
	{
		if(Multiplayer.IsServer())
		{
			int PlayerID = Multiplayer.GetRemoteSenderId();
			SpawnPlayer(PlayerID);
		}
		else
		{
			RpcId(1, "RequestSpawn");
		}
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void SpawnPlayer(int PlayerID)
	{
		if(Multiplayer.IsServer())
		{
			PackedScene PlayerPS = GD.Load<PackedScene>("res://player/Player.tscn");
			Player PlayerScene = (Player)PlayerPS.Instantiate();
			PlayerScene.Name = PlayerID.ToString();
			PlayerScene.NUID = PlayerID;
			GetTree().Root.GetNode<Node3D>("Map/Players").AddChild(PlayerScene);
			RpcId(PlayerID, "SpawnPlayer", PlayerID);
		}
		else
		{
			PackedScene PlayerPS = GD.Load<PackedScene>("res://player/Player.tscn");
			Player PlayerScene = (Player)PlayerPS.Instantiate();
			PlayerScene.Name = PlayerID.ToString();
			GetTree().Root.GetNode<Node3D>("Map/Players").AddChild(PlayerScene);
		}
	}

	[RPC(TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SendPlayerInput(string CInputsText)
	{
		RpcId(1, "SetPlayerInput", CInputsText, Clock);
		GD.Print("Sending Player Inputs");
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer)]
	public void SetPlayerInput(string PlayerInputsText, int ClientTime)
	{
		int PlayerID = Multiplayer.GetRemoteSenderId();
		GetTree().Root.GetNode<Player>("Map/Players/" + PlayerID).SetInput(PlayerInputsText, ClientTime);
		GD.Print("Server Recieved Player Inputs");
	}

	[RPC(TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SendPlayerPos(int PNUID, string PlayerTransformText)
	{
		RpcId(0, "SetPlayerPos", PNUID, PlayerTransformText, Clock);
		GD.Print("Sending Player POS");
	}

	[RPC(MultiplayerAPI.RPCMode.AnyPeer)]
	public void SetPlayerPos(int PlayerID, string PlayerTransformText, int ServerTime)
	{
		GetTree().Root.GetNode<Player>("Map/Players/" + PlayerID).SetPos(PlayerTransformText, ServerTime);
		GD.Print("Client Recieved POS");
	}
}
