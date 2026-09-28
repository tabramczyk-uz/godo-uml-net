using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLFileFormatTests
{
	[Theory]
	[InlineData(Error.FileNotFound, "The file or its folder does not exist.")]
	[InlineData(Error.FileNoPermission, "You do not have permission to access it.")]
	[InlineData(Error.Unauthorized, "You do not have permission to access it.")]
	[InlineData(Error.FileAlreadyInUse, "Another program is using it.")]
	[InlineData(Error.FileCantWrite, "It could not be written. The disk may be full or read-only.")]
	public void DescribesFileErrorsInPlainWords(Error error, string description)
	{
		Assert.Equal(description, UMLFileFormat.DescribeError(error));
	}

	[Fact]
	public void NamesTheCodeOfErrorsItHasNoWordsFor()
	{
		Assert.Equal("Something went wrong (OutOfMemory).", UMLFileFormat.DescribeError(Error.OutOfMemory));
	}

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
