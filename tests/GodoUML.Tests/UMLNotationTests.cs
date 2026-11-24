using Xunit;

namespace GodoUML.Tests;

public class UMLNotationTests
{
	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData(UMLNodeType.Interface, "«interface»")]
	[InlineData(UMLNodeType.Enum, "«enumeration»")]
	public void NamesTheStereotypeOfInterfacesAndEnumerations(UMLNodeType type, string stereotype)
	{
		Assert.Equal(stereotype, UMLNotation.GetStereotype(type));
	}

	[Theory]
	[InlineData(UMLNodeType.Node)]
	[InlineData(UMLNodeType.Class)]
	[InlineData(UMLNodeType.AbstractClass)]
	[InlineData(UMLNodeType.UseCase)]
	[InlineData(UMLNodeType.Actor)]
	public void GivesOtherNodeTypesNoStereotype(UMLNodeType type)
	{
		Assert.Null(UMLNotation.GetStereotype(type));
	}
}
