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
	public void PutsNodesJoinedOnlyByAssociationsAndDependenciesOnAnArcBelowTheFirst()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "A --> B", "B ..> C", "C -- A");

		Vector2 a = CellOf(diagram, "A");
		Vector2 b = CellOf(diagram, "B");
		Vector2 c = CellOf(diagram, "C");
		Assert.True(b.Y > a.Y && c.Y > a.Y);
		Assert.Equal(b.Y, c.Y, 0.01f);
		Assert.True(b.X < a.X && a.X < c.X);
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

	private static float Column(int column) => UMLAutoLayout.Margin + (column * UMLAutoLayout.ColumnSpacing);

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
		Assert.Equal(Row(5), diagram.FindNode("Loose").Position.Value.Y);
	}

	private static string[] Classes(int count, string prefix = "C") =>
		Enumerable.Range(1, count).Select(i => $"class {prefix}{i}").ToArray();

	[Fact]
	public void PutsManyUnconnectedNodesInAGridRatherThanOneRow()
	{
		UMLDiagram diagram = Build(Classes(10));

		int columns = diagram.Nodes.Select(node => node.Position.Value.X).Distinct().Count();
		int rows = diagram.Nodes.Select(node => node.Position.Value.Y).Distinct().Count();
		Assert.True(columns >= UMLAutoLayout.MinimumGridColumns);
		Assert.True(rows > 1);
		Assert.Equal(10, diagram.Nodes.Select(node => node.Position.Value).Distinct().Count());
	}

	[Fact]
	public void FillsTheUnconnectedGridRowByRowInDeclarationOrder()
	{
		UMLDiagram diagram = Build(Classes(6));

		Assert.Equal(new Vector2(Column(0), Row(0)), diagram.FindNode("C1").Position);
		Assert.Equal(new Vector2(Column(3), Row(0)), diagram.FindNode("C4").Position);
		Assert.Equal(new Vector2(Column(0), Row(1)), diagram.FindNode("C5").Position);
	}

	[Fact]
	public void PutsUnconnectedNodesBelowEverythingElse()
	{
		UMLDiagram diagram = Build(["class Loose", "class Base", "class Derived", "Derived --|> Base"]);

		Assert.Equal(Row(0), diagram.FindNode("Base").Position.Value.Y);
		Assert.Equal(Row(1), diagram.FindNode("Derived").Position.Value.Y);
		Assert.Equal(new Vector2(Column(0), Row(3)), diagram.FindNode("Loose").Position);
	}

	/// <summary>
	/// <paramref name="count"/> separate groups, each a subclass under its own
	/// base: <c>B1</c> above <c>D1</c>, <c>B2</c> above <c>D2</c>, …
	/// </summary>
	private static string[] SeparateHierarchies(int count) =>
		Enumerable
			.Range(1, count)
			.SelectMany(i => new[] { $"class B{i}", $"class D{i}", $"D{i} --|> B{i}" })
			.ToArray();

	private static Vector2 CellOf(UMLDiagram diagram, string name)
	{
		Vector2 position = diagram.FindNode(name).Position.Value - (Vector2.One * UMLAutoLayout.Margin);
		return new Vector2(position.X / UMLAutoLayout.ColumnSpacing, position.Y / UMLAutoLayout.RowSpacing);
	}

	[Fact]
	public void KeepsAnEmptyCellBetweenSeparateGroups()
	{
		UMLDiagram diagram = Build(SeparateHierarchies(6));

		for (int i = 1; i <= 6; i++)
		{
			for (int j = i + 1; j <= 6; j++)
			{
				foreach (string a in new[] { $"B{i}", $"D{i}" })
				{
					foreach (string b in new[] { $"B{j}", $"D{j}" })
					{
						Vector2 apart = (CellOf(diagram, a) - CellOf(diagram, b)).Abs();
						Assert.True(apart.X >= 2 || apart.Y >= 2, $"{a} and {b} are only {apart} cells apart");
					}
				}
			}
		}
	}

	[Fact]
	public void PacksManyGroupsOnSeveralShelves()
	{
		UMLDiagram diagram = Build(SeparateHierarchies(6));

		Assert.True(diagram.Nodes.Select(node => node.Position.Value.Y).Distinct().Count() > 2);
		Assert.Equal(new Vector2(0, 0), CellOf(diagram, "B1"));
		Assert.Equal(new Vector2(2, 0), CellOf(diagram, "B2"));
	}

	[Fact]
	public void PutsTheGridOfUnconnectedNodesBelowEveryShelf()
	{
		UMLDiagram diagram = Build([.. SeparateHierarchies(6), "class Loose"]);

		float lowestInAGroup = diagram.Nodes.Where(node => node.Name != "Loose").Max(node => node.Position.Value.Y);
		Assert.Equal(lowestInAGroup + (2 * UMLAutoLayout.RowSpacing), diagram.FindNode("Loose").Position.Value.Y);
		Assert.Equal(Column(0), diagram.FindNode("Loose").Position.Value.X);
	}

	[Fact]
	public void NeverWrapsTheSubclassesOfOneBase()
	{
		UMLDiagram diagram = Build(
			[.. Classes(12, "Sub"), "class Base", .. Enumerable.Range(1, 12).Select(i => $"Sub{i} --|> Base")]
		);

		IEnumerable<UMLNode> subclasses = diagram.Nodes.Where(node => node.Name != "Base");
		Assert.Single(subclasses.Select(node => node.Position.Value.Y).Distinct());
	}

	[Fact]
	public void StacksALongChainOfInheritanceInOneColumn()
	{
		UMLDiagram diagram = Build(
			[.. Classes(10), .. Enumerable.Range(2, 9).Select(i => $"C{i} --|> C{i - 1}")]
		);

		Assert.Single(diagram.Nodes.Select(node => node.Position.Value.X).Distinct());
		Assert.Equal(10, diagram.Nodes.Select(node => node.Position.Value.Y).Distinct().Count());
	}

	[Fact]
	public void KeepsANodeAssociatedWithAHierarchyInItsGroup()
	{
		UMLDiagram diagram = Build(
			"class Friend",
			"class Base",
			"class Derived",
			"Derived --|> Base",
			"Friend --> Base"
		);

		Assert.Equal(Row(0), diagram.FindNode("Friend").Position.Value.Y);
		Assert.Equal(1, Mathf.Abs(CellOf(diagram, "Friend").X - CellOf(diagram, "Base").X));
	}

	[Fact]
	public void CountsANodeRelatedOnlyToItselfAsUnconnected()
	{
		UMLDiagram diagram = Build("class Base", "class Derived", "class Self", "Derived --|> Base", "Self --> Self");

		Assert.Equal(Row(3), diagram.FindNode("Self").Position.Value.Y);
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

	/// <summary>
	/// A class <c>Hub</c> associated with <paramref name="count"/> others,
	/// <c>N1</c>, <c>N2</c>, …, declared after it.
	/// </summary>
	private static string[] Star(int count) =>
		[
			"class Hub",
			.. Classes(count, "N"),
			.. Enumerable.Range(1, count).Select(i => $"Hub -- N{i}"),
		];

	/// <summary>Where <paramref name="name"/> is relative to <c>Hub</c>, in cells.</summary>
	private static Vector2 FromHub(UMLDiagram diagram, string name) =>
		CellOf(diagram, name) - CellOf(diagram, "Hub");

	/// <summary>Positions are rounded to whole pixels, which moves them by a fraction of a cell.</summary>
	private const float CellTolerance = 0.01f;

	[Fact]
	public void PutsThreeAssociatesOnAnArcBelowTheirHub()
	{
		UMLDiagram diagram = Build(Star(3));

		Assert.Equal(0, FromHub(diagram, "N1").X, CellTolerance);
		Assert.True(FromHub(diagram, "N1").Y > 0);
		Assert.True(FromHub(diagram, "N2").X < 0 && FromHub(diagram, "N2").Y > 0);
		Assert.True(FromHub(diagram, "N3").X > 0 && FromHub(diagram, "N3").Y > 0);
	}

	[Fact]
	public void PutsTheOnlyAssociateStraightBelowItsHub()
	{
		UMLDiagram diagram = Build(Star(1));

		Assert.Equal(new Vector2(0, 1), FromHub(diagram, "N1"));
	}

	[Fact]
	public void PutsTwelveAssociatesOnAFullRingOneCellApart()
	{
		UMLDiagram diagram = Build(Star(12));

		List<Vector2> ring = Enumerable.Range(1, 12).Select(i => FromHub(diagram, $"N{i}")).ToList();
		Assert.All(ring, cell => Assert.Equal(ring[0].Length(), cell.Length(), 0.02f));
		Assert.Contains(ring, cell => cell.Y < 0);
		Assert.Contains(ring, cell => cell.Y > 0);
		for (int i = 0; i < ring.Count; i++)
		{
			for (int j = i + 1; j < ring.Count; j++)
			{
				Assert.True(ring[i].DistanceTo(ring[j]) >= 1 - CellTolerance, $"N{i + 1} and N{j + 1} are too close");
			}
		}
	}

	[Fact]
	public void PutsTheLargestSubtreeBottomMost()
	{
		UMLDiagram diagram = Build([.. Star(3), "class Far1", "class Far2", "N3 -- Far1", "N3 -- Far2"]);

		Assert.Equal(0, FromHub(diagram, "N3").X, CellTolerance);
		Assert.True(FromHub(diagram, "N3").Y > FromHub(diagram, "N1").Y);
		Assert.True(FromHub(diagram, "N3").Y > FromHub(diagram, "N2").Y);
	}

	[Fact]
	public void DoesNotStringAChainOfAssociationsAlongOneRow()
	{
		UMLDiagram diagram = Build([.. Classes(10), .. Enumerable.Range(2, 9).Select(i => $"C{i} -- C{i - 1}")]);

		List<Vector2> cells = diagram.Nodes.Select(node => CellOf(diagram, node.Name)).ToList();
		Assert.True(cells.Max(cell => cell.X) - cells.Min(cell => cell.X) < 9);
		Assert.True(cells.Select(cell => cell.Y).Distinct().Count() > 1);
	}

	/// <summary>Whether any of <paramref name="lines"/> runs through a node other than its two ends.</summary>
	private static void AssertLinesClear(UMLDiagram diagram, IEnumerable<(string From, string To)> lines)
	{
		foreach ((string from, string to) in lines)
		{
			foreach (UMLNode node in diagram.Nodes.Where(node => node.Name != from && node.Name != to))
			{
				Assert.False(LineCrosses(diagram, from, to, node.Name), $"{from}–{to} runs through {node.Name}");
			}
		}
	}

	private static IEnumerable<(string From, string To)> LinesOf(UMLDiagram diagram) =>
		diagram.Relationships.Select(relationship => (relationship.From.Name, relationship.To.Name));

	[Theory]
	[InlineData(3)]
	[InlineData(7)]
	[InlineData(8)]
	[InlineData(12)]
	[InlineData(20)]
	public void KeepsTheLinesOfAStarClearOfItsNodes(int count)
	{
		UMLDiagram diagram = Build(Star(count));

		AssertLinesClear(diagram, LinesOf(diagram));
	}

	[Fact]
	public void KeepsTheLinesOfAChainClearOfItsNodes()
	{
		UMLDiagram diagram = Build([.. Classes(10), .. Enumerable.Range(2, 9).Select(i => $"C{i} -- C{i - 1}")]);

		AssertLinesClear(diagram, LinesOf(diagram));
	}

	[Fact]
	public void KeepsTheLinesOfATwoLevelTreeClearOfItsNodes()
	{
		UMLDiagram diagram = Build(
			[
				.. Star(5),
				.. Enumerable
					.Range(1, 5)
					.SelectMany(i => Enumerable.Range(1, i).SelectMany(j => new[] { $"class L{i}x{j}", $"N{i} -- L{i}x{j}" })),
			]
		);

		AssertLinesClear(diagram, LinesOf(diagram));
	}

	[Fact]
	public void KeepsTheSpanningTreeLinesOfTwoHubsSharingNeighboursClear()
	{
		UMLDiagram diagram = Build(
			[.. Star(4), "class Other", .. Enumerable.Range(1, 4).Select(i => $"Other -- N{i}")]
		);

		// Hub is declared first, so the tree runs from it through its four
		// neighbours to Other, which hangs off N1; Other's lines to N2–N4 close
		// cycles and are left to task 3.4.
		AssertLinesClear(diagram, [("Hub", "N1"), ("Hub", "N2"), ("Hub", "N3"), ("Hub", "N4"), ("N1", "Other")]);
	}

	[Fact]
	public void NeverOverlapsNodesOfDifferentGroups()
	{
		UMLDiagram diagram = Build(
			[
				.. Star(9),
				.. SeparateHierarchies(3),
				.. Classes(5, "Loose"),
				"class P",
				"class Q",
				"P -- Q",
			]
		);

		List<Rect2> boxes = diagram
			.Nodes.Select(node => new Rect2(node.Position.Value, UMLAutoLayout.EstimatedNodeSize))
			.ToList();
		for (int i = 0; i < boxes.Count; i++)
		{
			for (int j = i + 1; j < boxes.Count; j++)
			{
				Assert.False(
					boxes[i].Intersects(boxes[j]),
					$"{diagram.Nodes[i].Name} overlaps {diagram.Nodes[j].Name}"
				);
			}
		}
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
