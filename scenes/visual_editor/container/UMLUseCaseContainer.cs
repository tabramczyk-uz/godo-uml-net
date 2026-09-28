using Godot;

/// <summary>
/// A use case: its name inside an ellipse instead of a box. The ellipse fills
/// the container, which the scene pads so the name clears the curve.
/// </summary>
public partial class UMLUseCaseContainer : UMLNodeContainer
{
	private const int OutlinePointCount = 64;

	public override void _Draw()
	{
		StyleBoxFlat box = GetBoxStyle();
		Vector2[] outline = UMLGeometry.GetEllipsePoints(
			new Rect2(Vector2.Zero, Size),
			OutlinePointCount
		);

		DrawColoredPolygon(outline, box.BgColor);
		DrawPolyline([.. outline, outline[0]], box.BorderColor, box.BorderWidthTop, true);
	}

	protected override string FormatName()
	{
		return $"[center]{UmlNode.Name}[/center]";
	}
}
