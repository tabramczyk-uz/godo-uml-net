using System.Collections.Generic;
using Godot;

public partial class UMLMethod : Resource
{
	public UMLMethod(
		string name = "method",
		string returnType = "void",
		UMLVisibility visibility = UMLVisibility.Unknown,
		List<UMLMethodArgument> arguments = null
	)
	{
		Name = name;
		ReturnType = returnType;
		Visibility = visibility;
		Arguments = arguments ?? new List<UMLMethodArgument>();
	}

	public string Name { get; set; }
	public string ReturnType { get; set; }
	public UMLVisibility Visibility { get; set; }
	public List<UMLMethodArgument> Arguments { get; set; }
}
