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
	public void KeepsNodesJoinedOnlyByAssociationsAndDependenciesInOneRow()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A --> B", "B ..> C", "C -- A");

		Assert.All(diagram.Nodes, node => Assert.Equal(UMLAutoLayout.Margin, node.Position.Value.Y));
	}

	[Fact]
	public void LetsAnAssociationOrderARow()
	{
		// X and Y share their parent P, so without the associations they would
		// keep declaration order, Y first. Each is pulled towards the node it is
		// associated with in the row above: X towards A on the left, Y towards B.
		UMLDiagram diagram = Build(
			"class A",
			"class B",
			"class P",
			"class Y",
			"class X",
			"Y --|> P",
			"X --|> P",
			"X --> A",
			"Y --> B"
		);

		Assert.Equal(diagram.FindNode("X").Position.Value.Y, diagram.FindNode("Y").Position.Value.Y);
		Assert.True(diagram.FindNode("X").Position.Value.X < diagram.FindNode("Y").Position.Value.X);
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
		UMLDiagram diagram = Build("class A", "class B", "class C", "A *-- B", "B *-- C", "C *-- A");

		Assert.Equal(3, diagram.Nodes.Count);
	}

	private static float Row(int row) => UMLAutoLayout.Margin + (row * UMLAutoLayout.RowSpacing);

	[Fact]
	public void StacksACycleOneNodePerRow()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A *-- B", "B *-- C", "C *-- A");

		Assert.Equal(Row(0), diagram.FindNode("A").Position.Value.Y);
		Assert.Equal(Row(1), diagram.FindNode("B").Position.Value.Y);
		Assert.Equal(Row(2), diagram.FindNode("C").Position.Value.Y);
	}

	[Fact]
	public void StacksTwoNodesPointingAtEachOther()
	{
		UMLDiagram diagram = Build("class A", "class B", "A *-- B", "B *-- A");

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
			"Top *-- A",
			"A *-- B",
			"B *-- A",
			"B *-- Bottom"
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
		UMLDiagram diagram = Build("class A", "class B", "class C", "A *-- B", "B *-- C", "C *-- A");

		Assert.False(LineCrosses(diagram, "C", "A", "B"));
	}

	[Fact]
	public void KeepsALineSkippingARowClearOfTheNodesInIt()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A *-- B", "B *-- C", "A *-- C");

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
			"A *-- B",
			"B *-- C",
			"C *-- D",
			"A *-- D"
		);

		Assert.False(LineCrosses(diagram, "A", "D", "B"));
		Assert.False(LineCrosses(diagram, "A", "D", "C"));
	}

	[Fact]
	public void BreaksACycleWhereverItIsDeclared()
	{
		UMLDiagram diagram = Build("class C", "class A", "class B", "A *-- B", "B *-- C", "C *-- A");

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

	/// <summary>
	/// Parses <paramref name="code"/> and places its unpositioned nodes, as
	/// the class canvas does on every parse.
	/// </summary>
	private static UMLDiagram Open(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		UMLAutoLayout.ApplyToUnpositioned(result.Diagram);
		return result.Diagram;
	}

	/// <summary>Reset Position on one node: its position line is deleted.</summary>
	private static string ResetPosition(string code, string name)
	{
		return UMLCodeWriter.RemoveNodePositions(code, [Open(code).FindNode(name)]);
	}

	private const string TwoPinnedNodes = "class A\n\tposition: [520, 380]\nclass B\n\tposition: [280, 210]";

	[Fact]
	public void ResettingTwoPinnedNodesPutsThemOnSeparateGridCells()
	{
		string code = ResetPosition(ResetPosition(TwoPinnedNodes, "B"), "A");
		UMLDiagram diagram = Open(code);

		Vector2 a = diagram.FindNode("A").Position.Value;
		Vector2 b = diagram.FindNode("B").Position.Value;
		Assert.True(diagram.FindNode("A").IsAutoPositioned);
		Assert.True(diagram.FindNode("B").IsAutoPositioned);
		Assert.NotEqual(a, b);
		Assert.True(
			Mathf.Abs(a.X - b.X) >= UMLAutoLayout.ColumnSpacing || Mathf.Abs(a.Y - b.Y) >= UMLAutoLayout.RowSpacing
		);

		// Resting on separate cells, the canvas has nothing to push apart.
		Rect2[] rest = [new(a, UMLAutoLayout.EstimatedNodeSize), new(b, UMLAutoLayout.EstimatedNodeSize)];
		Assert.Equal([a, b], UMLAutoLayout.Separate(rest, []));
	}

	[Fact]
	public void ResettingNodesGivesTheSamePositionsWhicheverOrderTheyAreResetIn()
	{
		UMLDiagram resetBFirst = Open(ResetPosition(ResetPosition(TwoPinnedNodes, "B"), "A"));
		UMLDiagram resetAFirst = Open(ResetPosition(ResetPosition(TwoPinnedNodes, "A"), "B"));

		Assert.Equal(
			resetBFirst.Nodes.Select(node => (node.Name, node.Position)),
			resetAFirst.Nodes.Select(node => (node.Name, node.Position))
		);
	}

	private static Vector2? PositionOf(UMLDiagram diagram, string name) => diagram.FindNode(name).Position;

	[Fact]
	public void PinningANodeLeavesTheOthersInTheirCells()
	{
		UMLDiagram before = Open("class A\nclass B\nclass C");
		UMLDiagram after = Open("class A\nclass B\n\tposition: [1000, 1000]\nclass C");

		Assert.Equal(PositionOf(before, "A"), PositionOf(after, "A"));
		Assert.Equal(PositionOf(before, "C"), PositionOf(after, "C"));
	}

	[Fact]
	public void ResettingANodePutsItBackWithoutMovingTheOthers()
	{
		const string code = "class A\nclass B\n\tposition: [1000, 1000]\nclass C";
		UMLDiagram pinned = Open(code);
		UMLDiagram reset = Open(ResetPosition(code, "B"));

		Assert.Equal(PositionOf(pinned, "A"), PositionOf(reset, "A"));
		Assert.Equal(PositionOf(pinned, "C"), PositionOf(reset, "C"));
		Assert.Equal(PositionOf(Open("class A\nclass B\nclass C"), "B"), PositionOf(reset, "B"));
	}

	[Fact]
	public void LeavesANodePinnedOnTopOfAnotherForTheCanvasToPushApart()
	{
		UMLDiagram diagram = Open("class A\nclass B\n\tposition: [60, 60]");

		Assert.Equal(new Vector2(UMLAutoLayout.Margin, UMLAutoLayout.Margin), PositionOf(diagram, "A"));
	}

	/// <summary>
	/// What the class canvas does with the auto-positioned nodes of
	/// <paramref name="diagram"/>: pushes them from where the layout put them
	/// clear of the <paramref name="fixedBoxes"/>.
	/// </summary>
	private static Vector2[] Push(UMLDiagram diagram, string[] autoPositioned, Rect2[] fixedBoxes)
	{
		Rect2[] rest = autoPositioned
			.Select(name => new Rect2(PositionOf(diagram, name).Value, UMLAutoLayout.EstimatedNodeSize))
			.ToArray();
		return UMLAutoLayout.Separate(rest, fixedBoxes);
	}

	[Fact]
	public void DroppingADraggedNodeLeavesTheOthersWhereTheDragPushedThem()
	{
		const string code = "class A\nclass B\nclass C\nA --> B";
		var dropAt = new Vector2(60, 60);
		Rect2[] dragged = [new(dropAt, UMLAutoLayout.EstimatedNodeSize)];

		UMLDiagram whileDragging = Open(code);
		Vector2[] pushedWhileDragging = Push(whileDragging, ["A", "C"], dragged);

		UMLDiagram dropped = Open(UMLCodeWriter.SetNodePosition(code, whileDragging.FindNode("B"), dropAt));
		Vector2[] pushedAfterDrop = Push(dropped, ["A", "C"], dragged);

		Assert.NotEqual(PositionOf(whileDragging, "A"), pushedWhileDragging[0]);
		Assert.Equal(pushedWhileDragging, pushedAfterDrop);
	}

	[Fact]
	public void SeparatesAutoPositionedNodesFromPositionedOnes()
	{
		UMLDiagram diagram = Open("class A\nclass B\n\tposition: [60, 60]");

		UMLAutoLayout.SeparateFromPositioned(diagram);

		Assert.True(
			AreClear(
				new Rect2(PositionOf(diagram, "B").Value, UMLAutoLayout.EstimatedNodeSize),
				new Rect2(PositionOf(diagram, "A").Value, UMLAutoLayout.EstimatedNodeSize)
			)
		);
		Assert.Equal(new Vector2(60, 60), PositionOf(diagram, "B"));
	}

	[Fact]
	public void ResettingANodeGivesTheSamePositionsAsOpeningTheResultingText()
	{
		UMLDiagram reset = Open(ResetPosition(TwoPinnedNodes, "B"));
		UMLDiagram opened = Open("class A\n\tposition: [520, 380]\nclass B");

		Assert.Equal(
			opened.Nodes.Select(node => (node.Name, node.Position)),
			reset.Nodes.Select(node => (node.Name, node.Position))
		);
	}
}
