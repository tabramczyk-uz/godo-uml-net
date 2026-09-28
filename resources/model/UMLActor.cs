using Godot;

/// <summary>
/// A stick figure of a use case diagram.
/// </summary>
public class UMLActor : UMLNode
{
	private static readonly PackedScene UmlActorContainer = GD.Load<PackedScene>(
		"uid://b8giogud0x4nj"
	);

	public UMLActor(string name = "Actor", Vector2? position = null)
		: base(UMLNodeType.Actor, name, position) { }

	public override UMLNodeContainer ToContainer()
	{
		UMLNodeContainer container = UmlActorContainer.Instantiate() as UMLNodeContainer;
		container.UmlNode = this;
		return container;
	}
}
