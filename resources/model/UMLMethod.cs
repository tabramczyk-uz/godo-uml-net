using System.Collections.Generic;

/// <summary>
/// One entry of a classifier's operation compartment.
/// <see cref="ReturnType"/> is empty when the source code leaves it out.
/// </summary>
public class UMLMethod
{
	public UMLMethod(
		string name = "method",
		string returnType = "",
		UMLVisibility visibility = UMLVisibility.Unknown,
		List<UMLMethodArgument> arguments = null
	)
	{
		Name = name;
		ReturnType = returnType ?? "";
		Visibility = visibility;
		Arguments = arguments ?? [];
	}

	public string Name { get; set; }
	public string ReturnType { get; set; }
	public UMLVisibility Visibility { get; set; }
	public List<UMLMethodArgument> Arguments { get; set; }
}
