using Godot;

public partial class Main : Control
{
	private UMLParser parser;
	private CodeEditor codeEditor;
	private VisualEditor visualEditor;

	private bool hasError = false;

	public override void _Ready()
	{
		parser = new UMLParser();
		parser.ErrorOccurred += OnParserError;

		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.CodeChanged += OnCodeChanged;

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
		visualEditor.NodeNameChanged += OnNodeNameChanged;
		visualEditor.NodePositionChanged += OnNodePositionChanged;
	}

	private void OnParserError(string message, int lineNumber)
	{
		// GD.PrintErr($"Error on line {lineNumber}: {message}");
		codeEditor.ShowError(message, lineNumber);
	}

	private void OnCodeChanged(string code)
	{
		codeEditor.DismissError();
		var diagram = parser.ParseCode(code);
		visualEditor.RenderDiagram(diagram);
	}

	private void OnNodeNameChanged(UMLNode node, string newName)
	{
		if (parser.IsNodeNameValid(newName))
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
