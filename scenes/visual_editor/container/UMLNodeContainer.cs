using System;
using Godot;

public partial class UMLNodeContainer : Control
{
	public event Action<UMLNodeContainer, Vector2> Dragged;

	public event Action<UMLNodeContainer> Dropped;

	public event Action<UMLNode, string> NameChanged;

	private UMLNode umlNode = new();
	public UMLNode UmlNode
	{
		get { return umlNode; }
		set
		{
			umlNode = value;
			Position = value.Position.Value;

			if (isReady) Update();
		}
	}

	private RichTextLabel nameLabel;
	private EditPopup editPopup;

	private bool isReady = false;
	private bool isEnabled = true;
	private bool isHeld = false;

	public void SetNode(UMLNode node)
	{
		UmlNode = node;
	}

	public virtual void Update() {
		nameLabel.Text = $"[center][b]{umlNode.Name}[/b][/center]";
	}

	public override void _Ready()
	{
		nameLabel = GetNode<RichTextLabel>("%Name");
		editPopup = GetNode<EditPopup>("%EditPopup");

		nameLabel.GuiInput += OnNameLabelInput;
		editPopup.EditFinished += OnEditFinished;
		Update();

		isReady = true;
	}

	public override void _Input(InputEvent @event)
	{
		if (!isEnabled)
		{
			return;
		}

		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				if (mouseEvent.Pressed && GetGlobalRect().HasPoint(mouseEvent.Position))
				{
					isHeld = true;
				}
				else if (!mouseEvent.Pressed)
				{
					isHeld = false;
					if (Position != umlNode.Position)
					{
						Dropped?.Invoke(this);
					}
				}
			}
		}
		else if (@event is InputEventMouseMotion motionEvent)
		{
			if (isHeld)
			{
				Dragged?.Invoke(this, motionEvent.Relative);
			}
		}
	}

	internal void ToggleInput(bool enabled)
	{
		isEnabled = enabled;
	}

	internal Vector2 GetConnectionPointPosition()
	{
		return GetGlobalRect().GetCenter();
	}

	private void OnNameLabelInput(InputEvent @event)
	{
		if (
			@event is InputEventMouseButton mouseEvent
			&& mouseEvent.Pressed
			&& mouseEvent.DoubleClick
			&& mouseEvent.ButtonIndex == MouseButton.Left
		)
		{
			editPopup.ShowAtMousePosition(umlNode.Name);
		}
	}

	private void OnEditFinished(string newName)
	{
		NameChanged?.Invoke(umlNode, newName);
	}
}
