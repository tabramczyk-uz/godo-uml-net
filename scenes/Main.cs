using Godot;

public partial class Main : Control
{
	private CodeEditor codeEditor;
	private VisualEditor visualEditor;

	public override void _Ready()
	{
		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.CodeChanged += OnCodeChanged;

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
		visualEditor.NodeNameChanged += OnNodeNameChanged;
		visualEditor.NodePositionChanged += OnNodePositionChanged;
	}

	private void OnCodeChanged(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		if (result.IsSuccess)
		{
			codeEditor.DismissError();
		}
		else
		{
			codeEditor.ShowError(result.ErrorMessage, result.ErrorLineNumber);
		}

		visualEditor.RenderDiagram(result.Diagram);
	}

	private void OnNodeNameChanged(UMLNode node, string newName)
	{
		if (UMLSyntax.IsValidNodeName(newName))
		{
			codeEditor.ChangeNodeName(node, newName);
		}
		else
		{
			// TODO: Show error message to user
			GD.PrintErr($"Invalid node name: {newName}");
		}
	}

	private void OnNodePositionChanged(UMLNode node, Vector2 newPosition)
	{
		codeEditor.ChangeNodePosition(node, newPosition);
	}
}
