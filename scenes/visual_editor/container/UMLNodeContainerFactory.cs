using Godot;

/// <summary>
/// Builds the container that draws a node on the canvas. The one place that
/// knows which container scene backs which node type, so the model under
/// <c>resources/</c> never has to load a scene.
/// </summary>
public static class UMLNodeContainerFactory
{
	private static readonly PackedScene NodeScene = GD.Load<PackedScene>("uid://255l5qlme474");
	private static readonly PackedScene ClassScene = GD.Load<PackedScene>("uid://miycnuypaj3e");
	private static readonly PackedScene UseCaseScene = GD.Load<PackedScene>("uid://dimcc2iq5rp8f");
	private static readonly PackedScene ActorScene = GD.Load<PackedScene>("uid://b8giogud0x4nj");

	public static UMLNodeContainer Create(UMLNode node)
	{
		return node.Type switch
		{
			UMLNodeType.UseCase => Instantiate(UseCaseScene, node),
			UMLNodeType.Actor => Instantiate(ActorScene, node),
			_ when node.Type.IsClassifier() => InstantiateClass((UMLClass)node),
			_ => Instantiate(NodeScene, node),
		};
	}

	private static UMLNodeContainer Instantiate(PackedScene scene, UMLNode node)
	{
		UMLNodeContainer container = scene.Instantiate<UMLNodeContainer>();
		container.UmlNode = node;
		return container;
	}

	private static UMLClassContainer InstantiateClass(UMLClass node)
	{
		UMLClassContainer container = ClassScene.Instantiate<UMLClassContainer>();
		container.UmlClass = node;
		return container;
	}
}
