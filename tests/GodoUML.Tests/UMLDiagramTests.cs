using Xunit;

namespace GodoUML.Tests;

public class UMLDiagramTests
{
	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	private static readonly UMLDiagram Shop = Parse("class Customer\nclass Order\nCustomer --> Order\n");

	[Fact]
	public void RejectsRenamingToANameAnotherNodeHas()
	{
		Assert.Equal(
			"There is already a node named \"Order\".",
			Shop.DescribeRenameProblem(Shop.FindNode("Customer"), "Order")
		);
	}

	[Theory]
	[InlineData("Client")]
	[InlineData("Customer")]
	[InlineData("order")]
	public void AllowsAFreeNameTheSameNameOrADifferentCase(string newName)
	{
		Assert.Null(Shop.DescribeRenameProblem(Shop.FindNode("Customer"), newName));
	}

	[Fact]
	public void ReportsAnInvalidNameBeforeLookingForDuplicates()
	{
		Assert.Equal(
			UMLSyntax.DescribeInvalidNodeName("My Order"),
			Shop.DescribeRenameProblem(Shop.FindNode("Customer"), "My Order")
		);
	}

	[Fact]
	public void KeepsARenameTheParserWouldRejectFromGettingThrough()
	{
		UMLNode customer = Shop.FindNode("Customer");
		Assert.NotNull(Shop.DescribeRenameProblem(customer, "Order"));

		string renamed = UMLCodeWriter.RenameNode("class Customer\nclass Order\nCustomer --> Order\n", customer, "Order");
		Assert.False(UMLParser.Parse(renamed).IsSuccess);
	}
}
