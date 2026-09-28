using Xunit;

namespace GodoUML.Tests;

public class UMLFileFormatTests
{
	[Theory]
	[InlineData("C:/diagrams/shop", "C:/diagrams/shop.guml")]
	[InlineData("C:/diagrams/shop.guml", "C:/diagrams/shop.guml")]
	[InlineData("C:/diagrams/shop.GUML", "C:/diagrams/shop.GUML")]
	[InlineData("C:/diagrams/shop.txt", "C:/diagrams/shop.txt.guml")]
	[InlineData("C:/diagrams.v2/shop", "C:/diagrams.v2/shop.guml")]
	public void AddsTheExtensionOnlyWhenItIsMissing(string path, string expected)
	{
		Assert.Equal(expected, UMLFileFormat.WithExtension(path));
	}

	[Fact]
	public void FiltersFileDialogsToGumlFiles()
	{
		Assert.Equal("*.guml ; GodoUML Diagram", UMLFileFormat.DialogFilter);
	}
}
