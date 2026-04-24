using Godot;

public class UMLNode : Resource
{
	public UMLNode(string name = "Node", Vector2? position = null)
	{
		Name = name;
		Position = position ?? Vector2.Zero;
	}

	public string Name { get; set; }
	public Vector2 Position { get; set; }
}
