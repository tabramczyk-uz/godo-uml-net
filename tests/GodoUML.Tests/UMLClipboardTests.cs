using System.Collections.Generic;
using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLClipboardTests
{
	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	private const string Shop =
		"class Customer\n\tposition: [10, 20]\n\t-name: String\n"
		+ "class Order\n\tposition: [200, 20]\n"
		+ "class Item\n\tposition: [200, 150]\n"
		+ "Customer --> Order\nOrder *-- Item\n";

	[Fact]
	public void CopiesTheNodesWithTheirMembersAndTheRelationshipsBetweenThem()
	{
		UMLDiagram diagram = Parse(Shop);

		string copied = UMLClipboard.Copy(diagram, [diagram.FindNode("Customer"), diagram.FindNode("Order")]);

		Assert.Equal(
			"class Customer\n\tposition: [10, 20]\n\t- name : String\n\nclass Order\n\tposition: [200, 20]\n\nCustomer --> Order\n",
			copied
		);
	}

	private static readonly Vector2 Step = new(24, 24);

	[Fact]
	public void PastesIntoAnotherDiagramUnderTheSameNamesAndWhereTheyWere()
	{
		UMLDiagram source = Parse(Shop);
		string copied = UMLClipboard.Copy(source, [source.FindNode("Customer"), source.FindNode("Order")]);

		Assert.True(UMLClipboard.TryPaste(copied, Parse("class Other\n\tposition: [500, 500]"), Step, out string code, out List<string> names));

		Assert.Equal(["Customer", "Order"], names);
		UMLDiagram pasted = Parse(code);
		Assert.Equal(new Vector2(10, 20), pasted.FindNode("Customer").Position);
		Assert.Equal(new Vector2(200, 20), pasted.FindNode("Order").Position);
		Assert.Single(((UMLClass)pasted.FindNode("Customer")).Attributes);
		Assert.Single(pasted.Relationships);
	}

	[Fact]
	public void StepsEachPasteClearOfTheNodesAlreadyThere()
	{
		string target = Shop;
		UMLDiagram source = Parse(Shop);
		string copied = UMLClipboard.Copy(source, [source.FindNode("Customer")]);

		Assert.True(UMLClipboard.TryPaste(copied, Parse(target), Step, out string first, out _));
		target = UMLCodeWriter.AppendBlock(target, first);
		Assert.True(UMLClipboard.TryPaste(copied, Parse(target), Step, out string second, out _));
		target = UMLCodeWriter.AppendBlock(target, second);

		UMLDiagram result = Parse(target);
		Assert.Equal(new Vector2(34, 44), result.FindNode("Customer2").Position);
		Assert.Equal(new Vector2(58, 68), result.FindNode("Customer3").Position);
	}

	[Fact]
	public void NumbersPastedNamesOnAndKeepsTheirRelationships()
	{
		UMLDiagram diagram = Parse(Shop + "class Order2\n");
		string copied = UMLClipboard.Copy(diagram, [diagram.FindNode("Customer"), diagram.FindNode("Order")]);

		Assert.True(UMLClipboard.TryPaste(copied, diagram, Vector2.Zero, out string code, out List<string> names));

		Assert.Equal(["Customer2", "Order3"], names);
		Assert.Contains("Customer2 --> Order3", code);

		string combined = UMLCodeWriter.AppendBlock(Shop + "class Order2\n", code);
		UMLDiagram result = Parse(combined);
		Assert.Equal(6, result.Nodes.Count);
		Assert.Equal(3, result.Relationships.Count);
	}

	[Fact]
	public void NumbersOnFromTheStemOfANumberedName()
	{
		UMLDiagram diagram = Parse("class Order2");

		Assert.True(UMLClipboard.TryPaste("class Order2", diagram, Vector2.Zero, out _, out List<string> names));

		Assert.Equal(["Order3"], names);
	}

	[Fact]
	public void LaysOutPastedNodesThatHaveNoPosition()
	{
		Assert.True(UMLClipboard.TryPaste("class A\nclass B\nA --> B", new UMLDiagram(), new Vector2(5, 5), out string code, out _));

		UMLDiagram pasted = Parse(code);
		Assert.All(pasted.Nodes, node => Assert.NotNull(node.Position));
		Assert.NotEqual(pasted.FindNode("A").Position, pasted.FindNode("B").Position);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   \n")]
	[InlineData("just some words")]
	[InlineData("A --> B")]
	[InlineData("// only a comment")]
	public void RefusesTextThatIsNotNodes(string text)
	{
		Assert.False(UMLClipboard.TryPaste(text, new UMLDiagram(), Vector2.Zero, out _, out _));
	}

	[Theory]
	[InlineData("", "class A\n", "class A\n")]
	[InlineData("class X\n", "class A\n", "class X\n\nclass A\n")]
	[InlineData("class X", "class A\n", "class X\n\nclass A\n")]
	public void AppendsABlockSetOffByABlankLine(string code, string block, string expected)
	{
		Assert.Equal(expected, UMLCodeWriter.AppendBlock(code, block));
	}
}
