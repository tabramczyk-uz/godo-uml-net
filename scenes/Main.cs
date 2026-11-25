using System;
using System.Collections.Generic;
using Godot;

// `partial` lets Godot's source generators add the glue code every C# node
// class needs.
public partial class Main : Control
{
	/// <summary>Ids of the File menu's items, as set in the scene.</summary>
	private enum FileMenuItem
	{
		SaveAs = 0,
		Open = 1,
		Save = 2,
		New = 3,
		ExportToPlantUML = 5,
		ImportFromPlantUML = 6,
	}

	private const string ImportText = "Import";
	private const string ImportAnywayText = "Import Anyway";

	/// <summary>The action of the unsaved-changes dialog's Don't Save button.</summary>
	private const string DiscardAction = "discard";

	/// <summary>The action of the export dialog's Copy button.</summary>
	private const string CopyAction = "copy";
	private const string CopyText = "Copy";
	private const string CopiedText = "Copied";

	private CodeEditor codeEditor;
	private ClassDiagramController classDiagram;

	/// <summary>The controller for each diagram kind, picked by the diagram header.</summary>
	private Dictionary<UMLDiagramKind, IDiagramController> diagramControllers;

	/// <summary>
	/// The controller whose canvas is showing: the one for the kind the header
	/// last named, which stays when a header error leaves the kind unknown.
	/// </summary>
	private IDiagramController activeDiagram;

	private PopupMenu fileMenu;
	private FileDialog openDialog;
	private FileDialog saveDialog;
	private ConfirmationDialog unsavedDialog;
	private AcceptDialog errorDialog;
	private AcceptDialog exportDialog;
	private TextEdit plantUmlText;
	private Button copyButton;
	private ConfirmationDialog importDialog;
	private TextEdit plantUmlInput;
	private Label importMessage;

	/// <summary>
	/// Whether the import dialog has already listed the lines the pasted
	/// PlantUML would lose, so pressing Import again goes ahead anyway.
	/// </summary>
	private bool skippedLinesShown = false;

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
		// "%Name" finds the node marked as a unique name in this scene (Access as
		// Unique Name in the editor), wherever it sits in the tree.
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

		errorDialog = GetNode<AcceptDialog>("%ErrorDialog");
		// Breaks a long file name too, where plain word wrapping would cut it off.
		errorDialog.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;

		exportDialog = GetNode<AcceptDialog>("%ExportDialog");
		plantUmlText = GetNode<TextEdit>("%PlantUMLText");
		copyButton = exportDialog.AddButton(CopyText, action: CopyAction);
		exportDialog.CustomAction += OnExportDialogCustomAction;

		importDialog = GetNode<ConfirmationDialog>("%ImportDialog");
		plantUmlInput = GetNode<TextEdit>("%PlantUMLInput");
		importMessage = GetNode<Label>("%ImportMessage");
		importDialog.Confirmed += OnImportConfirmed;
		plantUmlInput.TextChanged += ResetImportDialog;

		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.CodeChanged += OnCodeChanged;

		classDiagram = new ClassDiagramController(
			codeEditor,
			GetNode<ClassDiagramCanvas>("%ClassDiagramCanvas")
		);
		var sequenceDiagram = new SequenceDiagramController(
			codeEditor,
			GetNode<SequenceDiagramCanvas>("%SequenceDiagramCanvas")
		);

		diagramControllers = new()
		{
			{ UMLDiagramKind.Class, classDiagram },
			{ UMLDiagramKind.Sequence, sequenceDiagram },
		};

		foreach (IDiagramController controller in diagramControllers.Values)
		{
			controller.ErrorReported += ShowErrorDialog;
			controller.SetActive(false);
		}

		// Code without a header is a class diagram, so that canvas shows first.
		activeDiagram = classDiagram;
		activeDiagram.SetActive(true);

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
			case FileMenuItem.ExportToPlantUML:
				ExportToPlantUML();
				break;
			case FileMenuItem.ImportFromPlantUML:
				ResetImportDialog();
				importDialog.PopupCentered();
				plantUmlInput.GrabFocus();
				break;
		}
	}

	/// <summary>
	/// Shows the diagram as PlantUML, read-only but selectable, ready to copy.
	/// It is exported from the code as it stands, with only the positions the
	/// code spells out, so a code error stops it rather than exporting an
	/// older diagram.
	/// </summary>
	private void ExportToPlantUML()
	{
		string code = codeEditor.Code;
		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		if (!header.IsSuccess)
		{
			ShowExportError(header.ErrorMessage, header.ErrorLineNumber);
			return;
		}

		if (header.Kind != UMLDiagramKind.Class)
		{
			ShowErrorDialog("Cannot Export", "Only class diagrams can be exported to PlantUML so far.");
			return;
		}

		UMLParseResult result = UMLParser.Parse(header.RemoveFrom(code));
		if (!result.IsSuccess)
		{
			ShowExportError(result.ErrorMessage, result.ErrorLineNumber);
			return;
		}

		plantUmlText.Text = PlantUMLExporter.Export(result.Diagram);
		copyButton.Text = CopyText;
		exportDialog.PopupCentered();
	}

	private void ShowExportError(string message, int lineNumber)
	{
		ShowErrorDialog(
			"Cannot Export",
			$"The code has an error on line {lineNumber + 1}: {message}\nFix it, then export again."
		);
	}

	private void OnExportDialogCustomAction(StringName action)
	{
		if (action == CopyAction)
		{
			DisplayServer.ClipboardSet(plantUmlText.Text);
			copyButton.Text = CopiedText;
		}
	}

	/// <summary>
	/// Imports the pasted PlantUML as a new, untitled diagram. Pasted text with
	/// nothing to import keeps the dialog open with a note saying so. Text the
	/// importer only partly understands first lists the lines it would lose,
	/// and imports on a second press.
	/// </summary>
	private void OnImportConfirmed()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(plantUmlInput.Text);
		if (result.IsEmpty)
		{
			ShowImportMessage(
				"There is nothing to import: no classes, interfaces, enums, use cases or actors were found."
			);
			return;
		}

		string skippedLines = result.DescribeSkippedLines();
		if (skippedLines != null && !skippedLinesShown)
		{
			ShowImportMessage(skippedLines);
			importDialog.OkButtonText = ImportAnywayText;
			skippedLinesShown = true;
			return;
		}

		importDialog.Hide();
		string code = UMLCodeGenerator.Generate(result.Diagram);
		ConfirmDiscardingChanges(
			"importing a diagram",
			() =>
			{
				ReplaceDiagram(code);
				plantUmlInput.Text = string.Empty;
			}
		);
	}

	private void ShowImportMessage(string message)
	{
		importMessage.Text = message;
		importMessage.Show();
	}

	/// <summary>Clears the import dialog's note, as when the pasted text changes.</summary>
	private void ResetImportDialog()
	{
		importMessage.Hide();
		importDialog.OkButtonText = ImportText;
		skippedLinesShown = false;
	}

	private void NewDiagram()
	{
		ReplaceDiagram(string.Empty);
	}

	/// <summary>
	/// Replaces the diagram with <paramref name="code"/> as a new, untitled
	/// one, so the next Save asks where to put it: empty for New, generated for
	/// an import, which is therefore unsaved. The Save As dialog stays in the
	/// same folder but forgets the old file name, so it cannot suggest
	/// overwriting that file.
	/// </summary>
	private void ReplaceDiagram(string code)
	{
		LoadCode(code);
		document = new UMLDocument();
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
			ShowFileError("Open", path, FileAccess.GetOpenError());
			return;
		}

		LoadCode(UMLFileFormat.NormalizeLineEndings(file.GetAsText()));
		document.MarkSaved(path, codeEditor.Code);
		RememberFile(path);
		UpdateTitle();
	}

	/// <summary>
	/// Puts <paramref name="code"/> in the editor in place of the old diagram,
	/// laid out afresh and framed on the canvas.
	/// </summary>
	private void LoadCode(string code)
	{
		classDiagram.ForgetLayout();
		codeEditor.LoadCode(code);
		FrameActiveDiagramLater();
	}

	/// <summary>
	/// Frames the diagram on the active canvas once the frame's layout has run.
	/// A canvas that has just been shown still has the size it had while
	/// hidden, if it ever had one, until its container sorts it, which Godot
	/// defers, and framing goes by that size.
	/// </summary>
	private void FrameActiveDiagramLater()
	{
		Callable.From(() => activeDiagram.FrameDiagram()).CallDeferred();
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
			ShowFileError("Save", path, FileAccess.GetOpenError());
			return false;
		}

		if (!file.StoreString(codeEditor.Code))
		{
			ShowFileError("Save", path, file.GetError());
			return false;
		}

		document.MarkSaved(path, codeEditor.Code);
		RememberFile(path);
		UpdateTitle();
		return true;
	}

	/// <summary>
	/// Tells the user that <paramref name="verb"/>, "Open" or "Save", failed
	/// for <paramref name="path"/>, and why, and logs it too.
	/// </summary>
	private void ShowFileError(string verb, string path, Error error)
	{
		GD.PrintErr($"Could not {verb.ToLowerInvariant()} {path}: {error}");

		ShowErrorDialog(
			$"Could Not {verb}",
			$"Could not {verb.ToLowerInvariant()} {path}.\n{UMLFileFormat.DescribeError(error)}"
		);
	}

	private void ShowErrorDialog(string title, string message)
	{
		errorDialog.Title = title;
		errorDialog.DialogText = message;

		// Deferred, because whatever reported the error, such as a file dialog,
		// may still be open, and only one dialog can hold the window at a time.
		errorDialog.CallDeferred(Window.MethodName.PopupCentered, Vector2I.Zero);
	}

	/// <summary>
	/// Reads the diagram header and hands the rest of the code to the
	/// controller for the kind it names, switching canvases when the kind
	/// changed. A header error is shown on the canvas already showing, since
	/// the kind is then unknown.
	/// </summary>
	private void OnCodeChanged(string code)
	{
		UpdateTitle();

		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		if (!header.IsSuccess)
		{
			codeEditor.ShowError(header.ErrorMessage, header.ErrorLineNumber);
			activeDiagram.ShowInvalid();
			return;
		}

		IDiagramController controller = diagramControllers[header.Kind];
		bool switched = controller != activeDiagram;
		if (switched)
		{
			activeDiagram.SetActive(false);
			controller.SetActive(true);
			activeDiagram = controller;
		}

		activeDiagram.ShowCode(header.RemoveFrom(code));

		// The other canvas has its own camera, last placed for whatever it
		// showed before, if anything.
		if (switched)
		{
			FrameActiveDiagramLater();
		}
	}
}
