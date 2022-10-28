using Godot;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public partial class Player : CharacterBody3D
{
	Server ServerGlobal;

	public int NUID;

	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	Vector2 inputDir;

	public bool IsFrozen = false;

	public const float WalkSpeed = 30.0f;
	public const float SprintSpeed = 50.0f;
	public bool IsSprinting = false;
	Vector3 velocity;

	List<bool> Inputs;

	bool HasReceivedInput;

	int LastPosTime;
	int LastInputTime;

	float MouseSensitivity = 0.05f;

	Node3D RotationHelper;

	public override void _Ready()
	{
		ServerGlobal = GetNode<Server>("/root/Server");
		RotationHelper = GetNode<Node3D>("RotationHelper");

		Inputs = new List<bool>();
		Inputs.Add(false);
		Inputs.Add(false);
		Inputs.Add(false);
		Inputs.Add(false);
		Inputs.Add(false);
		Inputs.Add(false);
	}

	public override void _PhysicsProcess(double delta)
	{
		inputDir = new Vector2();
		velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
			velocity.y -= gravity * (float)delta;

		if(!IsFrozen)
		{
			ProcessInputs();
		}

		ProcessMovement(delta);

		for(int i = 0; i < Inputs.Count; i++)
		{
			Inputs[i] = false;
		}
	}

	void ProcessInputs()
	{
		if(Multiplayer.IsServer())
		{
			if(Inputs[0] == true && IsOnFloor())
			{
				velocity.y = JumpVelocity;
			}

			if(Inputs[1] == true)
			{
				IsSprinting = true;
			}
			else
			{
				IsSprinting = false;
			}

			if(Inputs[2] == true)
			{
				inputDir.x += 1;
			}

			if(Inputs[3] == true)
			{
				inputDir.x -= 1;
			}

			if(Inputs[4] == true)
			{
				inputDir.y -= 1;
			}

			if(Inputs[5] == true)
			{
				inputDir.y += 1;
			}
		}
		else
		{
			if (Input.IsActionJustPressed("movement_jump") && IsOnFloor())
			{
				velocity.y = JumpVelocity;
				Inputs[0] = true;
				GD.Print("JUMP");
			}

			if(Input.IsActionPressed("movement_sprint"))
			{
				IsSprinting = true;
				Inputs[1] = true;
			}
			else
			{
				IsSprinting = false;
				Inputs[1] = false;
			}

			if(Input.IsActionPressed("movement_right"))
			{
				inputDir.x += 1;
				Inputs[2] = true;
			}

			if(Input.IsActionPressed("movement_left"))
			{
				inputDir.x -= 1;
				Inputs[3] = true;
			}

			if(Input.IsActionPressed("movement_backward"))
			{
				inputDir.y -= 1;
				Inputs[4] = true;
			}

			if(Input.IsActionPressed("movement_forward"))
			{
				inputDir.y += 1;
				Inputs[5] = true;
			}

			if(Input.IsActionJustPressed("ui_cancel"))
			{

			}
		}
	}

	void ProcessMovement(double delta)
	{
		Vector3 direction = (Transform.basis * new Vector3(inputDir.x, 0, inputDir.y)).Normalized();
		if (direction != Vector3.Zero)
		{
			if(IsSprinting == true)
			{
				velocity.x = direction.x * SprintSpeed;
				velocity.z = direction.z * SprintSpeed;
			}
			else
			{
				velocity.x = direction.x * WalkSpeed;
				velocity.z = direction.z * WalkSpeed;
			}
		}
		else
		{
			if(IsSprinting == true)
			{
				velocity.x = Mathf.MoveToward(Velocity.x, 0, SprintSpeed);
				velocity.z = Mathf.MoveToward(Velocity.z, 0, SprintSpeed);
			}
			else
			{
				velocity.x = Mathf.MoveToward(Velocity.x, 0, WalkSpeed);
				velocity.z = Mathf.MoveToward(Velocity.z, 0, WalkSpeed);
			}
		}

		if(Multiplayer.IsServer())
		{
			if(HasReceivedInput)
			{
				GetPOS();
			}
		}
		else
		{
			GetInputs();
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	public override void _Input(InputEvent @event)
    {
		if(!Multiplayer.IsServer())
		{
			if (@event is InputEventMouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        	{
            	InputEventMouseMotion mouseEvent = @event as InputEventMouseMotion;
            	RotationHelper.RotateX(Mathf.DegToRad(mouseEvent.Relative.y * MouseSensitivity));
            	RotateY(Mathf.DegToRad(-mouseEvent.Relative.x * MouseSensitivity));

				//Hopefully this is right
            	//Vector3 cameraRot = RotationHelper.RotationDegrees;
				Vector3 cameraRot = RotationHelper.Rotation;
            	cameraRot.x = Mathf.Clamp(cameraRot.x, -70, 70);
            	//RotationHelper.RotationDegrees = cameraRot;
				RotationHelper.Rotation = cameraRot;
        	}
		}
    }

	void GetPOS()
	{
		string PlayerTransformText = JsonConvert.SerializeObject(this.Position, Formatting.Indented);
		ServerGlobal.SendPlayerPos(NUID, PlayerTransformText);
	}

	void GetInputs()
	{
		string InputsText = JsonConvert.SerializeObject(Inputs, Formatting.Indented);
		ServerGlobal.SendPlayerInput(InputsText);
		GD.Print("Got Inputs?");
		GD.Print(InputsText);
	}

	public void SetInput(string PlayerInputsText, int ClientTime)
	{
		if(HasReceivedInput)
		{
			if(ClientTime > LastInputTime)
			{
				LastInputTime = ClientTime;
				Inputs = JsonConvert.DeserializeObject<List<bool>>(PlayerInputsText);
			}
		}
		else
		{
			if(ClientTime > LastInputTime)
			{
				LastInputTime = ClientTime;
				Inputs = JsonConvert.DeserializeObject<List<bool>>(PlayerInputsText);
			}
			HasReceivedInput = true;
		}
		GD.Print("Setting Inputs");
	}

	public void SetPos(string PlayerTransformText, int ServerTime)
	{
		if(ServerTime > LastPosTime)
		{
			LastPosTime = ServerTime;
			Position = JsonConvert.DeserializeObject<Vector3>(PlayerTransformText);
		}
		GD.Print("Setting POS");
	}
}
