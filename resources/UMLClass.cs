using System.Collections.Generic;

public class UMLClass : UMLNode
{
	public UMLClass(string name = "Class", List<UMLAttribute> attributes = null, List<UMLMethod> methods = null) : base(name)
	{
		Attributes = attributes ?? new List<UMLAttribute>();
		Methods = methods ?? new List<UMLMethod>();
	}

	public List<UMLAttribute> Attributes { get; set; }
	public List<UMLMethod> Methods { get; set; }
}
