using Godot;

public partial class Main : Control
{
	/// <summary>Ids of the File menu's items, as set in the scene.</summary>
	private enum FileMenuItem
	{
		Save,
	}

	private CodeEditor codeEditor;
	private VisualEditor visualEditor;
	private FileDialog saveDialog;

	public override void _Ready()
	{
		GetNode<PopupMenu>("%File").IdPressed += OnFileMenuIdPressed;

		saveDialog = GetNode<FileDialog>("%SaveDialog");
		saveDialog.Filters = [UMLFileFormat.DialogFilter];
		saveDialog.FileSelected += OnSaveFileSelected;

		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.CodeChanged += OnCodeChanged;

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
		visualEditor.NodeNameChanged += OnNodeNameChanged;
		visualEditor.NodePositionChanged += OnNodePositionChanged;
		visualEditor.NodeAdded += OnNodeAdded;
		visualEditor.RelationshipAdded += OnRelationshipAdded;
		visualEditor.RelationshipRemoved += OnRelationshipRemoved;
	}

	private void OnFileMenuIdPressed(long id)
	{
		switch ((FileMenuItem)id)
		{
			case FileMenuItem.Save:
				saveDialog.PopupCentered();
				break;
		}
	}

	/// <summary>
	/// Writes the source code, exactly as the editor holds it, to the chosen
	/// file, adding the <c>.guml</c> extension if the name left it out.
	/// </summary>
	private void OnSaveFileSelected(string path)
	{
		path = UMLFileFormat.WithExtension(path);

		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			// TODO: Show error message to user
			GD.PrintErr($"Could not save {path}: {FileAccess.GetOpenError()}");
			return;
		}

		file.StoreString(codeEditor.Code);

		// The next save starts from the file just written.
		saveDialog.CurrentPath = path;
	}

	private void OnCodeChanged(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		if (result.IsSuccess)
		{
			codeEditor.DismissError();
			UMLAutoLayout.ApplyToUnpositioned(result.Diagram);
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

	private void OnNodeAdded(UMLNodeType type, string name, Vector2 position)
	{
		codeEditor.AddNode(type, name, position);
	}

	private void OnRelationshipAdded(
		UMLNode from,
		UMLNode to,
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		codeEditor.AddRelationship(from, to, type, direction);
	}

	private void OnRelationshipRemoved(UMLRelationship relationship)
	{
		codeEditor.RemoveRelationship(relationship);
	}
}
