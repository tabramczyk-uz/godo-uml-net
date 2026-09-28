using Godot;

/// <summary>
/// An actor: a stick figure with its name underneath, and no box around
/// either.
/// </summary>
public partial class UMLActorContainer : UMLNodeContainer
{
	private Control figure;

	public override void _Ready()
	{
		figure = GetNode<Control>("%Figure");
		figure.Draw += DrawFigure;

		base._Ready();
	}

	protected override string FormatName()
	{
		return $"[center]{UmlNode.Name}[/center]";
	}

	/// <summary>
	/// Draws the stick figure into the space the scene keeps for it above the
	/// name, in the colors and line width of the other nodes' boxes.
	/// </summary>
	private void DrawFigure()
	{
		StyleBoxFlat box = GetBoxStyle();
		Color color = box.BorderColor;
		float width = box.BorderWidthTop;
		Vector2 size = figure.Size;

		float headRadius = size.X / 4.0f;
		Vector2 head = new(size.X / 2.0f, headRadius + width);
		Vector2 neck = head + Vector2.Down * headRadius;
		Vector2 hip = new(size.X / 2.0f, size.Y * 0.62f);
		float shoulderY = neck.Y + (hip.Y - neck.Y) * 0.35f;

		figure.DrawCircle(head, headRadius, box.BgColor);
		figure.DrawArc(head, headRadius, 0.0f, Mathf.Tau, 32, color, width, true);
		figure.DrawLine(neck, hip, color, width, true);
		figure.DrawLine(new Vector2(width, shoulderY), new Vector2(size.X - width, shoulderY), color, width, true);
		figure.DrawLine(hip, new Vector2(width, size.Y - width), color, width, true);
		figure.DrawLine(hip, new Vector2(size.X - width, size.Y - width), color, width, true);
	}
}
