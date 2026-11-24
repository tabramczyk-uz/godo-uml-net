using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The class diagram's side of the round trip, between the shared code
/// editor and the class canvas. It parses the code, lays out and shows the
/// result, and turns every canvas edit into a code edit through
/// <see cref="UMLCodeWriter"/>. The shell (<see cref="Main"/>) only tells it
/// when the code changed. See ADR-0001.
/// </summary>
public class ClassDiagramController
{
	/// <summary>
	/// Raised with a title and a message when a canvas edit is refused, such
	/// as a rename to a name already taken, for the shell to show.
	/// </summary>
	public event Action<string, string> ErrorReported;

	private readonly CodeEditor codeEditor;
	private readonly ClassDiagramCanvas canvas;

	/// <summary>
	/// The last diagram the code parsed into, which is the one on the canvas:
	/// a failed parse leaves it in place, grayed out.
	/// </summary>
	private UMLDiagram diagram = new();

	public ClassDiagramController(CodeEditor codeEditor, ClassDiagramCanvas canvas)
	{
		this.codeEditor = codeEditor;
		this.canvas = canvas;

		canvas.NodeNameChanged += OnNodeNameChanged;
		canvas.NodesMoved += OnNodesMoved;
		canvas.NodePositionsReset += OnNodePositionsReset;
		canvas.NodeAdded += OnNodeAdded;
		canvas.NodesPasted += OnNodesPasted;
		canvas.RelationshipAdded += OnRelationshipAdded;
		canvas.RelationshipRemoved += OnRelationshipRemoved;
		canvas.NodesRemoved += OnNodesRemoved;
		canvas.UndoRequested += codeEditor.Undo;
		canvas.RedoRequested += codeEditor.Redo;
	}

	/// <summary>
	/// Parses <paramref name="code"/> and shows it on the canvas, or, if it has
	/// an error, marks the error in the code editor and grays out the canvas.
	/// </summary>
	public void ShowCode(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		if (result.IsSuccess)
		{
			codeEditor.DismissError();
			UMLAutoLayout.ApplyToUnpositioned(result.Diagram);
			diagram = result.Diagram;
		}
		else
		{
			codeEditor.ShowError(result.ErrorMessage, result.ErrorLineNumber);
		}

		canvas.RenderDiagram(result.Diagram);
	}

	/// <summary>
	/// Drops the remembered auto-layout positions, before a different diagram
	/// replaces this one, so it is laid out afresh.
	/// </summary>
	public void ForgetLayout()
	{
		canvas.ForgetLayout();
	}

	/// <summary>Fits the whole diagram on the canvas, as after opening a file.</summary>
	public void FrameDiagram()
	{
		canvas.FrameDiagram();
	}

	private void OnNodeNameChanged(UMLNode node, string newName)
	{
		string problem = diagram.DescribeRenameProblem(node, newName);
		if (problem == null)
		{
			codeEditor.ApplyEdit(UMLCodeWriter.RenameNode(codeEditor.Code, node, newName));
		}
		else
		{
			ErrorReported?.Invoke("Invalid Name", $"{node.Name} was not renamed.\n{problem}");
		}
	}

	/// <summary>
	/// Writes where the moved nodes now sit, all in one edit, so the code is
	/// parsed once, since each parse rebuilds every node on the canvas.
	/// </summary>
	private void OnNodesMoved(IReadOnlyDictionary<UMLNode, Vector2> positions)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.SetNodePositions(codeEditor.Code, positions));
	}

	/// <summary>
	/// Deletes the nodes' position lines in one edit, so the layout places
	/// them again, and the code is parsed once.
	/// </summary>
	private void OnNodePositionsReset(IReadOnlyList<UMLNode> nodes)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.RemoveNodePositions(codeEditor.Code, nodes));
	}

	private void OnNodeAdded(UMLNodeType type, string name, Vector2 position)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.AddNode(codeEditor.Code, type, name, position));
	}

	/// <summary>Appends the pasted nodes' code in one edit.</summary>
	private void OnNodesPasted(string block)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.AppendBlock(codeEditor.Code, block));
	}

	private void OnRelationshipAdded(
		UMLNode from,
		UMLNode to,
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.AddRelationship(codeEditor.Code, from, to, type, direction));
	}

	private void OnRelationshipRemoved(UMLRelationship relationship)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.RemoveRelationship(codeEditor.Code, relationship));
	}

	/// <summary>
	/// Deletes the nodes, and every relationship touching them, in one edit,
	/// so the code is parsed once.
	/// </summary>
	private void OnNodesRemoved(IReadOnlyList<UMLNode> nodes)
	{
		codeEditor.ApplyEdit(UMLCodeWriter.RemoveNodes(codeEditor.Code, nodes));
	}
}
