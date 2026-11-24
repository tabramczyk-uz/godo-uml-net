using System.Collections.Generic;
using Godot;
using Xunit;

namespace GodoUML.Tests;

/// <summary>
/// The engine-free side of <see cref="RelationshipLines"/>: where lines run
/// and which one a point picks. Drawing needs Godot and is checked by hand.
/// </summary>
public class RelationshipLinesTests
{
	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	/// <summary>Lines between boxes given by node name.</summary>
	private static RelationshipLines LinesBetween(Dictionary<string, Rect2> boxes)
	{
		return new RelationshipLines(node => boxes[node.Name]);
	}

	private static void AssertNear(Vector2 expected, Vector2 actual)
	{
		Assert.Equal(expected.X, actual.X, 4);
		Assert.Equal(expected.Y, actual.Y, 4);
	}

	[Fact]
	public void RunsBetweenTheFacingSidesOfTwoBoxes()
	{
		UMLDiagram diagram = Parse("class A\nclass B\nA --> B\n");
		RelationshipLines lines = LinesBetween(new()
		{
			["A"] = new Rect2(0.0f, 0.0f, 100.0f, 50.0f),
			["B"] = new Rect2(300.0f, 0.0f, 100.0f, 50.0f),
		});

		Assert.True(lines.TryGetEdges(diagram.Relationships[0], out Vector2 fromEdge, out Vector2 toEdge));
		AssertNear(new Vector2(100.0f, 25.0f), fromEdge);
		AssertNear(new Vector2(300.0f, 25.0f), toEdge);
	}

	[Fact]
	public void MeetsAUseCaseAtItsEllipse()
	{
		// Diagonally below-right of the use case, the line leaves the ellipse
		// before it reaches the bounding box's corner.
		UMLDiagram diagram = Parse("actor User\nusecase Order\nUser -- Order\n");
		Rect2 useCase = new(0.0f, 0.0f, 200.0f, 100.0f);
		RelationshipLines lines = LinesBetween(new()
		{
			["User"] = new Rect2(390.0f, 290.0f, 20.0f, 20.0f),
			["Order"] = useCase,
		});

		Assert.True(lines.TryGetEdges(diagram.Relationships[0], out _, out Vector2 toEdge));
		AssertNear(UMLGeometry.ClipToEllipse(useCase, new Vector2(400.0f, 300.0f)), toEdge);
		Assert.True(toEdge.X < 200.0f && toEdge.Y < 100.0f);
	}

	[Fact]
	public void HasNoLineBetweenBoxesOnTopOfEachOther()
	{
		UMLDiagram diagram = Parse("class A\nclass B\nA --> B\n");
		Rect2 box = new(0.0f, 0.0f, 100.0f, 50.0f);
		RelationshipLines lines = LinesBetween(new() { ["A"] = box, ["B"] = box });

		Assert.False(lines.TryGetEdges(diagram.Relationships[0], out _, out _));
		Assert.Null(lines.GetRelationshipAt(diagram.Relationships, box.GetCenter(), 1000.0f));
	}

	private static readonly UMLDiagram Triangle = Parse("class A\nclass B\nclass C\nA --> B\nA --> C\n");

	/// <summary>
	/// A on the left, B to its right and C below it, so A → B runs along
	/// y = 25 from x = 100 to 300, and A → C along x = 50 from y = 50 to 300.
	/// </summary>
	private static readonly RelationshipLines TriangleLines = LinesBetween(new()
	{
		["A"] = new Rect2(0.0f, 0.0f, 100.0f, 50.0f),
		["B"] = new Rect2(300.0f, 0.0f, 100.0f, 50.0f),
		["C"] = new Rect2(0.0f, 300.0f, 100.0f, 50.0f),
	});

	[Fact]
	public void PicksTheLineClosestToThePoint()
	{
		Assert.Same(
			Triangle.Relationships[0],
			TriangleLines.GetRelationshipAt(Triangle.Relationships, new Vector2(200.0f, 28.0f), 6.0f)
		);
		Assert.Same(
			Triangle.Relationships[1],
			TriangleLines.GetRelationshipAt(Triangle.Relationships, new Vector2(46.0f, 200.0f), 6.0f)
		);
	}

	[Fact]
	public void PicksTheNearerOfTwoLinesInReach()
	{
		// Two parallel lines, along y = 10 and y = 40, both in reach of a point
		// between them.
		UMLDiagram diagram = Parse("class A\nclass B\nclass C\nclass D\nA --> B\nC --> D\n");
		RelationshipLines lines = LinesBetween(new()
		{
			["A"] = new Rect2(0.0f, 0.0f, 100.0f, 20.0f),
			["B"] = new Rect2(300.0f, 0.0f, 100.0f, 20.0f),
			["C"] = new Rect2(0.0f, 30.0f, 100.0f, 20.0f),
			["D"] = new Rect2(300.0f, 30.0f, 100.0f, 20.0f),
		});

		Assert.Same(
			diagram.Relationships[0],
			lines.GetRelationshipAt(diagram.Relationships, new Vector2(200.0f, 22.0f), 20.0f)
		);
		Assert.Same(
			diagram.Relationships[1],
			lines.GetRelationshipAt(diagram.Relationships, new Vector2(200.0f, 27.0f), 20.0f)
		);
	}

	[Fact]
	public void PicksNothingFartherThanTheGivenDistance()
	{
		Assert.Null(TriangleLines.GetRelationshipAt(Triangle.Relationships, new Vector2(200.0f, 32.0f), 6.0f));
	}
}
