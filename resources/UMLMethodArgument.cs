using Godot;

public partial class UMLMethodArgument : Resource
{
	public UMLMethodArgument(string name = "argument", string type = "Integer")
	{
		Name = name;
		Type = type;
	}

	public string Name { get; set; }
	public string Type { get; set; }
}
