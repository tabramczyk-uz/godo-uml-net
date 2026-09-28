using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLGeometryTests
{
	private static readonly Vector2 Start = new(0.0f, 0.0f);
	private static readonly Vector2 End = new(100.0f, 0.0f);

	[Fact]
	public void MeasuresStraightAcrossToThePointsBetweenTheEnds()
	{
		Assert.Equal(5.0f, UMLGeometry.DistanceToSegment(new Vector2(40.0f, 5.0f), Start, End), 4);
		Assert.Equal(5.0f, UMLGeometry.DistanceToSegment(new Vector2(40.0f, -5.0f), Start, End), 4);
	}

	[Fact]
	public void MeasuresToTheNearestEndBeyondTheSegment()
	{
		Assert.Equal(5.0f, UMLGeometry.DistanceToSegment(new Vector2(-3.0f, 4.0f), Start, End), 4);
		Assert.Equal(5.0f, UMLGeometry.DistanceToSegment(new Vector2(103.0f, -4.0f), Start, End), 4);
	}

	[Fact]
	public void IsZeroOnTheSegment()
	{
		Assert.Equal(0.0f, UMLGeometry.DistanceToSegment(new Vector2(70.0f, 0.0f), Start, End), 4);
	}

	[Fact]
	public void HandlesADiagonalSegment()
	{
		Vector2 end = new(10.0f, 10.0f);

		Assert.Equal(
			Mathf.Sqrt(2.0f) * 5.0f,
			UMLGeometry.DistanceToSegment(new Vector2(10.0f, 0.0f), Start, end),
			4
		);
	}

	[Fact]
	public void TreatsAZeroLengthSegmentAsAPoint()
	{
		Assert.Equal(5.0f, UMLGeometry.DistanceToSegment(new Vector2(3.0f, 4.0f), Start, Start), 4);
	}

	private static readonly Rect2 Box = new(0.0f, 0.0f, 200.0f, 100.0f);

	/// <summary>
	/// 1 for a point on the outline of the ellipse inscribed in <see cref="Box"/>.
	/// </summary>
	private static float EllipseEquation(Vector2 point)
	{
		Vector2 relative = (point - Box.GetCenter()) / (Box.Size / 2.0f);
		return relative.LengthSquared();
	}

	[Fact]
	public void ClipsToTheEllipseAlongTheAxes()
	{
		Assert.Equal(new Vector2(200.0f, 50.0f), UMLGeometry.ClipToEllipse(Box, new Vector2(300.0f, 50.0f)));
		Assert.Equal(new Vector2(100.0f, 100.0f), UMLGeometry.ClipToEllipse(Box, new Vector2(100.0f, 200.0f)));
	}

	[Fact]
	public void ClipsToTheEllipseOnADiagonal()
	{
		Vector2 clipped = UMLGeometry.ClipToEllipse(Box, new Vector2(300.0f, 250.0f));

		Assert.Equal(1.0f, EllipseEquation(clipped), 4);
		Assert.Equal(clipped.X - 100.0f, clipped.Y - 50.0f, 4);
	}

	[Fact]
	public void DoesNotClipPastATargetInsideTheEllipse()
	{
		Vector2 inside = new(110.0f, 50.0f);

		Assert.Equal(inside, UMLGeometry.ClipToEllipse(Box, inside));
		Assert.Equal(Box.GetCenter(), UMLGeometry.ClipToEllipse(Box, Box.GetCenter()));
	}

	[Fact]
	public void PlacesEllipsePointsOnTheOutline()
	{
		Vector2[] points = UMLGeometry.GetEllipsePoints(Box, 32);

		Assert.Equal(32, points.Length);
		Assert.Equal(new Vector2(200.0f, 50.0f), points[0]);
		Assert.All(points, point => Assert.Equal(1.0f, EllipseEquation(point), 4));
	}
}
