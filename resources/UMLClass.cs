using System.Collections.Generic;

public partial class UMLClass : UMLNode
{
	public UMLClass(
		string name = "Class",
		List<UMLAttribute> attributes = null,
		List<UMLMethod> methods = null
	)
		: base(name)
	{
		Attributes = attributes ?? [];
		Methods = methods ?? [];
	}

	public List<UMLAttribute> Attributes { get; set; }
	public List<UMLMethod> Methods { get; set; }
}
