using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLAutoLayoutTests
{
	private static UMLDiagram Build(params string[] lines)
	{
		UMLParseResult result = UMLParser.Parse(string.Join("\n", lines));
		Assert.True(result.IsSuccess, result.ErrorMessage);
		UMLAutoLayout.Apply(result.Diagram);
		return result.Diagram;
	}

	[Fact]
	public void HandlesAnEmptyDiagram()
	{
		var diagram = new UMLDiagram();

		UMLAutoLayout.Apply(diagram);

		Assert.Empty(diagram.Nodes);
	}

	[Fact]
	public void SpreadsUnrelatedNodesAcrossOneRow()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C");

		Assert.All(diagram.Nodes, node => Assert.Equal(UMLAutoLayout.Margin, node.Position.Value.Y));
		Assert.Equal(3, diagram.Nodes.Select(node => node.Position.Value.X).Distinct().Count());
	}

	[Fact]
	public void PutsASuperclassAboveItsSubclasses()
	{
		UMLDiagram diagram = Build("class Base", "class Left", "class Right", "Left --|> Base", "Right --|> Base");

		float baseY = diagram.FindNode("Base").Position.Value.Y;
		Assert.True(diagram.FindNode("Left").Position.Value.Y > baseY);
		Assert.True(diagram.FindNode("Right").Position.Value.Y > baseY);
		Assert.Equal(diagram.FindNode("Left").Position.Value.Y, diagram.FindNode("Right").Position.Value.Y);
	}

	[Fact]
	public void ReadsAGeneralizationWrittenTheOtherWayRound()
	{
		UMLDiagram diagram = Build("class Base", "class Derived", "Base <|-- Derived");

		Assert.True(diagram.FindNode("Derived").Position.Value.Y > diagram.FindNode("Base").Position.Value.Y);
	}

	[Fact]
	public void PutsTheWholeAboveItsParts()
	{
		UMLDiagram diagram = Build("class Car", "class Wheel", "Car \"1\" *-- \"4\" Wheel");

		Assert.True(diagram.FindNode("Wheel").Position.Value.Y > diagram.FindNode("Car").Position.Value.Y);
	}

	[Fact]
	public void StacksAChainOfInheritance()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "B --|> A", "C --|> B");

		Assert.Equal(UMLAutoLayout.Margin, diagram.FindNode("A").Position.Value.Y);
		Assert.Equal(UMLAutoLayout.Margin + UMLAutoLayout.RowSpacing, diagram.FindNode("B").Position.Value.Y);
		Assert.Equal(
			UMLAutoLayout.Margin + (2 * UMLAutoLayout.RowSpacing),
			diagram.FindNode("C").Position.Value.Y
		);
	}

	[Fact]
	public void TerminatesOnACycle()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A --> B", "B --> C", "C --> A");

		Assert.Equal(3, diagram.Nodes.Count);
	}

	[Fact]
	public void NeverPutsTwoNodesInTheSamePlace()
	{
		UMLDiagram diagram = Build(
			"class A",
			"class B",
			"class C",
			"class D",
			"class E",
			"B --|> A",
			"C --|> A",
			"D --|> A",
			"E --> B"
		);

		List<Vector2> positions = diagram.Nodes.Select(node => node.Position.Value).ToList();
		Assert.Equal(positions.Count, positions.Distinct().Count());
	}

	private static readonly Vector2 BoxSize = new(100.0f, 50.0f);

	private static Rect2 Box(float x, float y) => new(new Vector2(x, y), BoxSize);

	private static bool AreClear(Rect2 a, Rect2 b) =>
		!a.Grow(UMLAutoLayout.ClearanceBuffer - 0.1f).Intersects(b);

	[Fact]
	public void LeavesSeparatedBoxesAlone()
	{
		Vector2[] positions = UMLAutoLayout.Separate([Box(0, 0), Box(500, 0)], [Box(0, 300)]);

		Assert.Equal([new Vector2(0, 0), new Vector2(500, 0)], positions);
	}

	[Fact]
	public void PushesAMovableBoxAwayFromAFixedOne()
	{
		Rect2 fixedBox = Box(0, 0);

		Vector2[] positions = UMLAutoLayout.Separate([Box(60, 10)], [fixedBox]);

		Assert.True(AreClear(fixedBox, new Rect2(positions[0], BoxSize)));
		Assert.True(positions[0].X > 60, "the box should keep moving the way it was pushed");
	}

	[Fact]
	public void SplitsThePushBetweenTwoMovableBoxes()
	{
		Vector2[] positions = UMLAutoLayout.Separate([Box(0, 0), Box(40, 0)], []);

		Assert.True(AreClear(new Rect2(positions[0], BoxSize), new Rect2(positions[1], BoxSize)));
		Assert.True(positions[0].X < 0);
		Assert.True(positions[1].X > 40);
	}

	[Fact]
	public void CascadesPushesThroughACrowd()
	{
		Rect2 pusher = Box(0, 0);
		Rect2[] crowd = [Box(30, 0), Box(150, 0), Box(270, 0)];

		Vector2[] positions = UMLAutoLayout.Separate(crowd, [pusher]);

		List<Rect2> boxes = [pusher, .. positions.Select(position => new Rect2(position, BoxSize))];
		for (int i = 0; i < boxes.Count; i++)
		{
			for (int j = i + 1; j < boxes.Count; j++)
			{
				Assert.True(AreClear(boxes[i], boxes[j]), $"boxes {i} and {j} still overlap");
			}
		}
	}

	[Fact]
	public void SeparatesBoxesThatShareACentre()
	{
		Vector2[] positions = UMLAutoLayout.Separate([Box(0, 0)], [Box(0, 0)]);

		Assert.True(AreClear(Box(0, 0), new Rect2(positions[0], BoxSize)));
	}

	[Fact]
	public void IsDeterministic()
	{
		string[] source = ["class A", "class B", "class C", "B --|> A", "C --|> A", "C --> B"];

		Assert.Equal(
			Build(source).Nodes.Select(node => node.Position),
			Build(source).Nodes.Select(node => node.Position)
		);
	}
}
