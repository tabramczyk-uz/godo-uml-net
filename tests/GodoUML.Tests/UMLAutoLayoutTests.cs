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

	private static float Row(int row) => UMLAutoLayout.Margin + (row * UMLAutoLayout.RowSpacing);

	[Fact]
	public void StacksACycleOneNodePerRow()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A --> B", "B --> C", "C --> A");

		Assert.Equal(Row(0), diagram.FindNode("A").Position.Value.Y);
		Assert.Equal(Row(1), diagram.FindNode("B").Position.Value.Y);
		Assert.Equal(Row(2), diagram.FindNode("C").Position.Value.Y);
	}

	[Fact]
	public void StacksTwoNodesPointingAtEachOther()
	{
		UMLDiagram diagram = Build("class A", "class B", "A --> B", "B --> A");

		Assert.Equal(Row(0), diagram.FindNode("A").Position.Value.Y);
		Assert.Equal(Row(1), diagram.FindNode("B").Position.Value.Y);
	}

	[Fact]
	public void LeavesNoEmptyRowsAroundACycle()
	{
		UMLDiagram diagram = Build(
			"class Top",
			"class A",
			"class B",
			"class Bottom",
			"class Loose",
			"Top --> A",
			"A --> B",
			"B --> A",
			"B --> Bottom"
		);

		Assert.Equal(Row(0), diagram.FindNode("Top").Position.Value.Y);
		Assert.Equal(Row(1), diagram.FindNode("A").Position.Value.Y);
		Assert.Equal(Row(2), diagram.FindNode("B").Position.Value.Y);
		Assert.Equal(Row(3), diagram.FindNode("Bottom").Position.Value.Y);
		Assert.Equal(Row(0), diagram.FindNode("Loose").Position.Value.Y);
	}

	/// <summary>
	/// Whether the straight line drawn between the centres of two nodes runs
	/// through a third, taking every node to be the estimated size.
	/// </summary>
	private static bool LineCrosses(UMLDiagram diagram, string from, string to, string through)
	{
		Vector2 half = UMLAutoLayout.EstimatedNodeSize / 2.0f;
		Vector2 start = diagram.FindNode(from).Position.Value + half;
		Vector2 end = diagram.FindNode(to).Position.Value + half;
		Rect2 box = new(diagram.FindNode(through).Position.Value, UMLAutoLayout.EstimatedNodeSize);

		for (int i = 0; i <= 100; i++)
		{
			if (box.HasPoint(start.Lerp(end, i / 100.0f)))
			{
				return true;
			}
		}

		return false;
	}

	[Fact]
	public void KeepsTheLineClosingACycleClearOfTheNodesInBetween()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A --> B", "B --> C", "C --> A");

		Assert.False(LineCrosses(diagram, "C", "A", "B"));
	}

	[Fact]
	public void KeepsALineSkippingARowClearOfTheNodesInIt()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A --> B", "B --> C", "A --> C");

		Assert.False(LineCrosses(diagram, "A", "C", "B"));
	}

	[Fact]
	public void KeepsALineSkippingSeveralRowsClearOfTheNodesInThem()
	{
		UMLDiagram diagram = Build(
			"class A",
			"class B",
			"class C",
			"class D",
			"A --> B",
			"B --> C",
			"C --> D",
			"A --> D"
		);

		Assert.False(LineCrosses(diagram, "A", "D", "B"));
		Assert.False(LineCrosses(diagram, "A", "D", "C"));
	}

	[Fact]
	public void BreaksACycleWhereverItIsDeclared()
	{
		UMLDiagram diagram = Build("class C", "class A", "class B", "A --> B", "B --> C", "C --> A");

		List<float> rows = diagram.Nodes.Select(node => node.Position.Value.Y).Distinct().ToList();
		Assert.Equal(3, rows.Count);
		Assert.Equal(Row(0), rows.Min());
		Assert.Equal(Row(2), rows.Max());
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
	public void DependsOnlyOnWhereThePusherIsNow()
	{
		Rect2[] rest = [Box(0, 0), Box(150, 0), Box(0, 120)];

		Vector2[] untouched = UMLAutoLayout.Separate(rest, [Box(600, 600)]);
		UMLAutoLayout.Separate(rest, [Box(40, 20)]);
		Vector2[] pushed = UMLAutoLayout.Separate(rest, [Box(40, 20)]);
		Vector2[] returned = UMLAutoLayout.Separate(rest, [Box(600, 600)]);

		Assert.Equal(rest.Select(box => box.Position), untouched);
		Assert.NotEqual(untouched, pushed);
		Assert.Equal(untouched, returned);
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
