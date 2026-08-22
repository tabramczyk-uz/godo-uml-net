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

		Assert.All(diagram.Nodes, node => Assert.Equal(UMLAutoLayout.Margin, node.Position.Y));
		Assert.Equal(3, diagram.Nodes.Select(node => node.Position.X).Distinct().Count());
	}

	[Fact]
	public void PutsASuperclassAboveItsSubclasses()
	{
		UMLDiagram diagram = Build("class Base", "class Left", "class Right", "Left --|> Base", "Right --|> Base");

		float baseY = diagram.FindNode("Base").Position.Y;
		Assert.True(diagram.FindNode("Left").Position.Y > baseY);
		Assert.True(diagram.FindNode("Right").Position.Y > baseY);
		Assert.Equal(diagram.FindNode("Left").Position.Y, diagram.FindNode("Right").Position.Y);
	}

	[Fact]
	public void ReadsAGeneralizationWrittenTheOtherWayRound()
	{
		UMLDiagram diagram = Build("class Base", "class Derived", "Base <|-- Derived");

		Assert.True(diagram.FindNode("Derived").Position.Y > diagram.FindNode("Base").Position.Y);
	}

	[Fact]
	public void PutsTheWholeAboveItsParts()
	{
		UMLDiagram diagram = Build("class Car", "class Wheel", "Car \"1\" *-- \"4\" Wheel");

		Assert.True(diagram.FindNode("Wheel").Position.Y > diagram.FindNode("Car").Position.Y);
	}

	[Fact]
	public void StacksAChainOfInheritance()
	{
		UMLDiagram diagram = Build("class A", "class B", "class C", "B --|> A", "C --|> B");

		Assert.Equal(UMLAutoLayout.Margin, diagram.FindNode("A").Position.Y);
		Assert.Equal(UMLAutoLayout.Margin + UMLAutoLayout.RowSpacing, diagram.FindNode("B").Position.Y);
		Assert.Equal(
			UMLAutoLayout.Margin + (2 * UMLAutoLayout.RowSpacing),
			diagram.FindNode("C").Position.Y
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

		List<Vector2> positions = diagram.Nodes.Select(node => node.Position).ToList();
		Assert.Equal(positions.Count, positions.Distinct().Count());
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
