using Godot;

public partial class UMLAttribute : Resource
{
	public UMLAttribute(
		string name = "attribute",
		string type = "Integer",
		UMLVisibility visibility = UMLVisibility.Unknown
	)
	{
		Name = name;
		Type = type;
		Visibility = visibility;
	}

	public string Name { get; set; }
	public string Type { get; set; }
	public UMLVisibility Visibility { get; set; }
}
