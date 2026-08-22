using Godot;

/// <summary>
/// An ellipse of a use case diagram.
/// </summary>
public class UMLUseCase : UMLNode
{
	public UMLUseCase(string name = "UseCase", Vector2? position = null)
		: base(UMLNodeType.UseCase, name, position) { }
}
