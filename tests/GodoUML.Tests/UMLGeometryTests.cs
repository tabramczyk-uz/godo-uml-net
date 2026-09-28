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
}
