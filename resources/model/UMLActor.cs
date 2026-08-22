using Godot;

/// <summary>
/// A stick figure of a use case diagram.
/// </summary>
public class UMLActor : UMLNode
{
	public UMLActor(string name = "Actor", Vector2? position = null)
		: base(UMLNodeType.Actor, name, position) { }
}
