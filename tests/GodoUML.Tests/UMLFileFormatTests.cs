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

	[Theory]
	[InlineData("class A\r\nclass B\r\n", "class A\nclass B\n")]
	[InlineData("class A\rclass B", "class A\nclass B")]
	[InlineData("class A\nclass B\n", "class A\nclass B\n")]
	[InlineData("class A\r\n\r\nclass B", "class A\n\nclass B")]
	public void NormalizesLineEndingsToLineFeeds(string text, string expected)
	{
		Assert.Equal(expected, UMLFileFormat.NormalizeLineEndings(text));
	}

	[Fact]
	public void ReadsAWindowsLineEndedFileAsTheSameDiagram()
	{
		string windows = "class Foo\r\n\tposition: [10, 20]\r\nclass Bar\r\nFoo --> Bar // uses\r\n";

		UMLParseResult result = UMLParser.Parse(UMLFileFormat.NormalizeLineEndings(windows));

		Assert.True(result.IsSuccess, result.ErrorMessage);
		Assert.Equal(2, result.Diagram.Nodes.Count);
		Assert.Single(result.Diagram.Relationships);
	}

	[Fact]
	public void FiltersFileDialogsToGumlFiles()
	{
		Assert.Equal("*.guml ; GodoUML Diagram", UMLFileFormat.DialogFilter);
	}
}
