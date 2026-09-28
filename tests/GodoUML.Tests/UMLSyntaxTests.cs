using Xunit;

namespace GodoUML.Tests;

public class UMLSyntaxTests
{
	[Theory]
	[InlineData("Customer")]
	[InlineData("_internal")]
	[InlineData("Order2")]
	[InlineData("snake_case_name")]
	public void FindsNothingWrongWithAValidName(string name)
	{
		Assert.Null(UMLSyntax.DescribeInvalidNodeName(name));
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void RejectsAnEmptyName(string name)
	{
		Assert.Equal("A node name cannot be empty.", UMLSyntax.DescribeInvalidNodeName(name));
	}

	[Fact]
	public void RejectsALeadingDigit()
	{
		Assert.Equal(
			"\"2Fast\" starts with a digit. Node names must start with a letter or an underscore.",
			UMLSyntax.DescribeInvalidNodeName("2Fast")
		);
	}

	[Theory]
	[InlineData("My Node", "a space")]
	[InlineData(" Padded", "a space")]
	[InlineData("Order-Line", "\"-\"")]
	[InlineData("Łódź", "\"Ł\"")]
	[InlineData("Café", "\"é\"")]
	public void PointsAtTheFirstCharacterANameCannotContain(string name, string what)
	{
		Assert.Equal(
			$"\"{name}\" contains {what}. Node names can only use the letters A-Z, digits and underscores.",
			UMLSyntax.DescribeInvalidNodeName(name)
		);
	}
}
