using System;
using System.Collections.Generic;
using Godot;

public partial class CodeEditor : Control
{
	public event Action<string> CodeChanged;

	[Export]
	public Color StringColor { get; set; } = new Color();

	[Export]
	public Color CommentColor { get; set; } = new Color();

	[Export]
	public Color ErrorColor { get; set; } = new Color();

	private TextEdit codeEdit;
	private Timer updateTimer;
	private MarginContainer errorContainer;
	private RichTextLabel errorLabel;

	private int errorLine = -1;

	/// <summary>The source code as it stands in the editor.</summary>
	public string Code => codeEdit.Text;

	public override void _Ready()
	{
		codeEdit = GetNode<TextEdit>("%CodeEdit");
		updateTimer = GetNode<Timer>("%UpdateTimer");
		errorContainer = GetNode<MarginContainer>("%ErrorContainer");
		errorLabel = GetNode<RichTextLabel>("%ErrorLabel");

		codeEdit.TextChanged += OnTextChanged;
		updateTimer.Timeout += SubmitCode;
	}

	private void SubmitCode()
	{
		CodeChanged?.Invoke(codeEdit.Text);
	}

	/// <summary>
	/// Replaces the whole source with <paramref name="code"/>, as when opening a
	/// file, and parses it straight away. Undo starts afresh, so it cannot bring
	/// back the previous diagram.
	/// </summary>
	public void LoadCode(string code)
	{
		codeEdit.Text = code;
		codeEdit.ClearUndoHistory();
		SubmitCode();
	}

	/// <summary>
	/// Steps back through the editor's history, which holds the canvas edits
	/// too, since each of those is an edit of the code. Parses straight away
	/// rather than after the typing delay.
	/// </summary>
	public void Undo()
	{
		codeEdit.Undo();
		SubmitCode();
	}

	/// <inheritdoc cref="Undo"/>
	public void Redo()
	{
		codeEdit.Redo();
		SubmitCode();
	}

	public void ChangeNodeName(UMLNode node, string newName)
	{
		codeEdit.Text = UMLCodeWriter.RenameNode(codeEdit.Text, node, newName);
		SubmitCode();
	}

	/// <summary>
	/// Writes where the moved nodes now sit, all in one edit, then parses once,
	/// since each parse rebuilds every node on the canvas.
	/// </summary>
	public void ChangeNodePositions(IReadOnlyDictionary<UMLNode, Vector2> positions)
	{
		codeEdit.Text = UMLCodeWriter.SetNodePositions(codeEdit.Text, positions);
		SubmitCode();
	}

	/// <summary>
	/// Deletes the nodes, and every relationship touching them, in one edit,
	/// then parses once.
	/// </summary>
	public void RemoveNodes(IReadOnlyList<UMLNode> nodes)
	{
		codeEdit.Text = UMLCodeWriter.RemoveNodes(codeEdit.Text, nodes);
		SubmitCode();
	}

	/// <summary>Appends a block of code, such as pasted nodes, in one edit.</summary>
	public void AppendBlock(string block)
	{
		codeEdit.Text = UMLCodeWriter.AppendBlock(codeEdit.Text, block);
		SubmitCode();
	}

	public void AddNode(UMLNodeType type, string name, Vector2 position)
	{
		codeEdit.Text = UMLCodeWriter.AddNode(codeEdit.Text, type, name, position);
		SubmitCode();
	}

	public void AddRelationship(
		UMLNode from,
		UMLNode to,
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		codeEdit.Text = UMLCodeWriter.AddRelationship(codeEdit.Text, from, to, type, direction);
		SubmitCode();
	}

	public void RemoveRelationship(UMLRelationship relationship)
	{
		codeEdit.Text = UMLCodeWriter.RemoveRelationship(codeEdit.Text, relationship);
		SubmitCode();
	}

	public void ShowError(string message, int lineNumber)
	{
		if (errorLine != -1)
		{
			codeEdit.SetLineBackgroundColor(errorLine, new Color(0, 0, 0, 0));
		}

		codeEdit.SetLineBackgroundColor(lineNumber, ErrorColor);
		errorLabel.Text = $"Error on line {lineNumber + 1}: {message}";
		errorContainer.Show();
		errorLine = lineNumber;
	}

	public void DismissError()
	{
		if (errorLine != -1 && errorLine < codeEdit.GetLineCount())
		{
			codeEdit.SetLineBackgroundColor(errorLine, new Color(0, 0, 0, 0));
		}

		errorContainer.Hide();
		errorLine = -1;
	}

	private void OnTextChanged()
	{
		updateTimer.Start();
	}
}
