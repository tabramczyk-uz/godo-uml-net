/// <summary>
/// Every kind of box the language can declare. The keyword that spells each one
/// out lives in <see cref="UMLSyntax"/>; the container scene that draws it lives
/// in <c>VisualEditor.AddUmlNode</c>.
/// </summary>
public enum UMLNodeType
{
	/// <summary>Plain rectangle, carrying nothing but a name.</summary>
	Node,

	/// <summary>Classifier with an attribute and an operation compartment.</summary>
	Class,

	/// <summary>Classifier drawn with the <c>«interface»</c> stereotype.</summary>
	Interface,

	/// <summary>Classifier drawn with an italic name.</summary>
	AbstractClass,

	/// <summary>Classifier drawn with the <c>«enumeration»</c> stereotype.</summary>
	Enum,

	/// <summary>Ellipse of a use case diagram.</summary>
	UseCase,

	/// <summary>Stick figure of a use case diagram.</summary>
	Actor,
}

public static class UMLNodeTypeExtensions
{
	/// <summary>
	/// True for the node types that own attribute and operation compartments,
	/// which is exactly the set backed by <see cref="UMLClass"/>.
	/// </summary>
	public static bool IsClassifier(this UMLNodeType nodeType)
	{
		return nodeType
			is UMLNodeType.Class
				or UMLNodeType.Interface
				or UMLNodeType.AbstractClass
				or UMLNodeType.Enum;
	}
}
