using Godot;

/// <summary>
/// An ellipse of a use case diagram.
/// </summary>
public class UMLUseCase : UMLNode
{
	private static readonly PackedScene UmlUseCaseContainer = GD.Load<PackedScene>(
		"uid://dimcc2iq5rp8f"
	);

	public UMLUseCase(string name = "UseCase", Vector2? position = null)
		: base(UMLNodeType.UseCase, name, position) { }

	public override UMLNodeContainer ToContainer()
	{
		UMLNodeContainer container = UmlUseCaseContainer.Instantiate() as UMLNodeContainer;
		container.UmlNode = this;
		return container;
	}
}
