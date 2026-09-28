using System;
using Godot;

public partial class Main : Control
{
	/// <summary>Ids of the File menu's items, as set in the scene.</summary>
	private enum FileMenuItem
	{
		SaveAs = 0,
		Open = 1,
		Save = 2,
		New = 3,
	}

	/// <summary>The action of the unsaved-changes dialog's Don't Save button.</summary>
	private const string DiscardAction = "discard";

	private CodeEditor codeEditor;
	private VisualEditor visualEditor;
	private PopupMenu fileMenu;
	private FileDialog openDialog;
	private FileDialog saveDialog;
	private ConfirmationDialog unsavedDialog;

	private UMLDocument document;

	/// <summary>
	/// What to do once the unsaved changes are saved or discarded, while the
	/// unsaved-changes dialog is open.
	/// </summary>
	private Action pendingAction = null;

	/// <summary>
	/// What to do once the Save As dialog has written the file, when that
	/// dialog was opened on the way to something else, such as closing.
	/// </summary>
	private Action afterSaveAs = null;

	public override void _Ready()
	{
		fileMenu = GetNode<PopupMenu>("%File");
		fileMenu.IdPressed += OnFileMenuIdPressed;
		SetShortcut(FileMenuItem.New, Key.N);
		SetShortcut(FileMenuItem.Open, Key.O);
		SetShortcut(FileMenuItem.Save, Key.S);
		SetShortcut(FileMenuItem.SaveAs, Key.S, shift: true);

		openDialog = GetNode<FileDialog>("%OpenDialog");
		openDialog.Filters = [UMLFileFormat.DialogFilter];
		openDialog.FileSelected += OnOpenFileSelected;

		saveDialog = GetNode<FileDialog>("%SaveDialog");
		saveDialog.Filters = [UMLFileFormat.DialogFilter];
		saveDialog.FileSelected += OnSaveFileSelected;
		saveDialog.Canceled += () => afterSaveAs = null;

		unsavedDialog = GetNode<ConfirmationDialog>("%UnsavedDialog");
		unsavedDialog.AddButton("Don't Save", right: true, action: DiscardAction);
		unsavedDialog.Confirmed += () => SaveThen(TakePendingAction());
		unsavedDialog.CustomAction += OnUnsavedDialogCustomAction;
		unsavedDialog.Canceled += () => pendingAction = null;

		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.CodeChanged += OnCodeChanged;

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
		visualEditor.NodeNameChanged += OnNodeNameChanged;
		visualEditor.NodePositionChanged += OnNodePositionChanged;
		visualEditor.NodeAdded += OnNodeAdded;
		visualEditor.RelationshipAdded += OnRelationshipAdded;
		visualEditor.RelationshipRemoved += OnRelationshipRemoved;

		document = new UMLDocument(codeEditor.Code);
		UpdateTitle();

		// Closing the window asks about unsaved changes first; see _Notification.
		GetTree().AutoAcceptQuit = false;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
		{
			ConfirmDiscardingChanges("closing", () => GetTree().Quit());
		}
	}

	private void SetShortcut(FileMenuItem item, Key key, bool shift = false)
	{
		var keyEvent = new InputEventKey
		{
			Keycode = key,
			ShiftPressed = shift,
			CommandOrControlAutoremap = true,
		};

		fileMenu.SetItemShortcut(fileMenu.GetItemIndex((int)item), new Shortcut { Events = [keyEvent] });
	}

	private void OnFileMenuIdPressed(long id)
	{
		switch ((FileMenuItem)id)
		{
			case FileMenuItem.New:
				ConfirmDiscardingChanges("starting a new diagram", NewDiagram);
				break;
			case FileMenuItem.Open:
				ConfirmDiscardingChanges("opening another diagram", () => openDialog.PopupCentered());
				break;
			case FileMenuItem.Save:
				SaveThen(null);
				break;
			case FileMenuItem.SaveAs:
				afterSaveAs = null;
				saveDialog.PopupCentered();
				break;
		}
	}

	/// <summary>
	/// Replaces the diagram with an empty, untitled one, so the next Save asks
	/// where to put it. The Save As dialog stays in the same folder but forgets
	/// the old file name, so it cannot suggest overwriting that file.
	/// </summary>
	private void NewDiagram()
	{
		visualEditor.ForgetLayout();
		codeEditor.LoadCode(string.Empty);
		visualEditor.FrameDiagram();
		document = new UMLDocument(codeEditor.Code);
		saveDialog.CurrentFile = string.Empty;
		UpdateTitle();
	}

	/// <summary>
	/// Runs <paramref name="action"/> straight away when there is nothing
	/// unsaved. Otherwise asks first whether to save the changes, discard them,
	/// or cancel, and runs it only after a save that succeeded or a discard.
	/// </summary>
	private void ConfirmDiscardingChanges(string reason, Action action)
	{
		if (!document.IsModified(codeEditor.Code))
		{
			action();
			return;
		}

		pendingAction = action;
		unsavedDialog.DialogText = $"Save changes to {document.Name} before {reason}?";
		unsavedDialog.PopupCentered();
	}

	private void OnUnsavedDialogCustomAction(StringName action)
	{
		if (action == DiscardAction)
		{
			Action discarded = TakePendingAction();
			unsavedDialog.Hide();
			discarded?.Invoke();
		}
	}

	/// <summary>
	/// Hands over the action waiting on the unsaved-changes dialog, so that
	/// however the dialog closes afterwards, it cannot run twice or linger.
	/// </summary>
	private Action TakePendingAction()
	{
		Action action = pendingAction;
		pendingAction = null;
		return action;
	}

	/// <summary>
	/// Saves to the file the diagram came from, or asks where to save it when it
	/// has none yet, then runs <paramref name="then"/> if the save went through.
	/// </summary>
	private void SaveThen(Action then)
	{
		if (document.Path == null)
		{
			afterSaveAs = then;
			saveDialog.PopupCentered();
			return;
		}

		if (WriteFile(document.Path))
		{
			then?.Invoke();
		}
	}

	private void UpdateTitle()
	{
		string appName = ProjectSettings.GetSetting("application/config/name").AsString();
		GetWindow().Title = document.GetTitle(codeEditor.Code, appName);
	}

	/// <summary>
	/// Replaces the diagram with the one in the chosen file, laid out afresh and
	/// framed on the canvas.
	/// </summary>
	private void OnOpenFileSelected(string path)
	{
		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			// TODO: Show error message to user
			GD.PrintErr($"Could not open {path}: {FileAccess.GetOpenError()}");
			return;
		}

		string code = UMLFileFormat.NormalizeLineEndings(file.GetAsText());

		visualEditor.ForgetLayout();
		codeEditor.LoadCode(code);
		visualEditor.FrameDiagram();
		document.MarkSaved(path, codeEditor.Code);
		RememberFile(path);
		UpdateTitle();
	}

	/// <summary>
	/// Starts both file dialogs from <paramref name="path"/> next time, so Save
	/// suggests the file that was opened and Open the one that was saved.
	/// </summary>
	private void RememberFile(string path)
	{
		openDialog.CurrentPath = path;
		saveDialog.CurrentPath = path;
	}

	/// <summary>
	/// Saves to the file chosen in the Save As dialog, adding the <c>.guml</c>
	/// extension if the name left it out, then carries on with whatever the
	/// dialog was opened on the way to.
	/// </summary>
	private void OnSaveFileSelected(string path)
	{
		Action then = afterSaveAs;
		afterSaveAs = null;

		if (WriteFile(UMLFileFormat.WithExtension(path)))
		{
			then?.Invoke();
		}
	}

	/// <summary>
	/// Writes the source code, exactly as the editor holds it, to
	/// <paramref name="path"/>, which becomes the diagram's file. Returns
	/// whether it worked.
	/// </summary>
	private bool WriteFile(string path)
	{
		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			// TODO: Show error message to user
			GD.PrintErr($"Could not save {path}: {FileAccess.GetOpenError()}");
			return false;
		}

		file.StoreString(codeEditor.Code);
		document.MarkSaved(path, codeEditor.Code);
		RememberFile(path);
		UpdateTitle();
		return true;
	}

	private void OnCodeChanged(string code)
	{
		UpdateTitle();

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
