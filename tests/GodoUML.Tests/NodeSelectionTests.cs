using Xunit;

namespace GodoUML.Tests;

public class NodeSelectionTests
{
	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	private static readonly UMLDiagram Shop = Parse("class Customer\nclass Order\nclass Item\n");
	private static readonly UMLNode Customer = Shop.FindNode("Customer");
	private static readonly UMLNode Order = Shop.FindNode("Order");
	private static readonly UMLNode Item = Shop.FindNode("Item");

	[Fact]
	public void PressingANodeSelectsItAlone()
	{
		NodeSelection selection = new();
		selection.Add(Customer);

		selection.Press(Order, addToSelection: false);

		Assert.False(selection.Contains(Customer));
		Assert.True(selection.Contains(Order));
	}

	[Fact]
	public void PressingWithShiftAddsToTheSelection()
	{
		NodeSelection selection = new();
		selection.Add(Customer);

		selection.Press(Order, addToSelection: true);

		Assert.True(selection.Contains(Customer));
		Assert.True(selection.Contains(Order));
	}

	[Fact]
	public void PressingASelectedNodeKeepsTheSelection()
	{
		NodeSelection selection = new();
		selection.Add(Customer);
		selection.Add(Order);

		selection.Press(Order, addToSelection: false);

		Assert.True(selection.Contains(Customer));
		Assert.True(selection.Contains(Order));
	}

	[Fact]
	public void OutlivesAReParse()
	{
		NodeSelection selection = new();
		selection.Add(Order);

		UMLDiagram reParsed = Parse("class Customer\nclass Order\nclass Item\n");

		Assert.True(selection.Contains(reParsed.FindNode("Order")));
		Assert.False(selection.Contains(reParsed.FindNode("Item")));
	}

	[Fact]
	public void DropsNodesTheNewDiagramNoLongerHas()
	{
		NodeSelection selection = new();
		selection.Add(Customer);
		selection.Add(Order);

		selection.RemoveMissing(Parse("class Customer\n"));
		UMLDiagram readded = Parse("class Customer\nclass Order\n");

		Assert.True(selection.Contains(readded.FindNode("Customer")));
		Assert.False(selection.Contains(readded.FindNode("Order")));
	}

	[Fact]
	public void ReplacingSelectsExactlyTheNamedNodes()
	{
		NodeSelection selection = new();
		selection.Add(Customer);

		selection.Replace(["Order", "Item"]);

		Assert.False(selection.Contains(Customer));
		Assert.True(selection.Contains(Order));
		Assert.True(selection.Contains(Item));
	}

	[Fact]
	public void ClearingReportsWhetherAnythingWasSelected()
	{
		NodeSelection selection = new();
		Assert.False(selection.Clear());

		selection.Add(Customer);
		Assert.True(selection.Clear());
		Assert.False(selection.Contains(Customer));
	}
}
