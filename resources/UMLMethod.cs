using Godot;
using System.Collections.Generic;

public class UMLMethod : Resource
{
	public UMLMethod(
		string name = "method",
		string returnType = "void",
		UMLParser.Visibility visibility = UMLParser.Visibility.Unknown,
		List<UMLMethodArgument> arguments = null)
	{
		Name = name;
		ReturnType = returnType;
		Visibility = visibility;
		Arguments = arguments ?? new List<UMLMethodArgument>();
	}

	public string Name { get; set; }
	public string ReturnType { get; set; }
	public UMLParser.Visibility Visibility { get; set; }
	public List<UMLMethodArgument> Arguments { get; set; }
}
