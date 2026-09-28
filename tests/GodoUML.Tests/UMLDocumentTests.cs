using Xunit;

namespace GodoUML.Tests;

public class UMLDocumentTests
{
	[Fact]
	public void StartsUntitledAndUnmodified()
	{
		var document = new UMLDocument();

		Assert.Null(document.Path);
		Assert.Equal("Untitled", document.Name);
		Assert.False(document.IsModified(""));
		Assert.True(document.IsModified("class A"));
	}

	[Fact]
	public void CountsUndoingBackToTheSavedCodeAsUnmodified()
	{
		var document = new UMLDocument();
		document.MarkSaved("C:/diagrams/shop.guml", "class A\n");

		Assert.True(document.IsModified("class A\nclass B\n"));
		Assert.False(document.IsModified("class A\n"));
	}

	[Fact]
	public void TakesItsNameFromTheSavedPath()
	{
		var document = new UMLDocument();
		document.MarkSaved("C:/diagrams/shop.guml", "class A\n");

		Assert.Equal("C:/diagrams/shop.guml", document.Path);
		Assert.Equal("shop.guml", document.Name);
	}

	[Fact]
	public void MarksUnsavedChangesInTheTitle()
	{
		var document = new UMLDocument();
		Assert.Equal("Untitled - GodoUML", document.GetTitle("", "GodoUML"));
		Assert.Equal("Untitled* - GodoUML", document.GetTitle("class A", "GodoUML"));

		document.MarkSaved("C:/diagrams/shop.guml", "class A");
		Assert.Equal("shop.guml - GodoUML", document.GetTitle("class A", "GodoUML"));
		Assert.Equal("shop.guml* - GodoUML", document.GetTitle("class B", "GodoUML"));
	}
}
