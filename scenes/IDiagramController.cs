using System;

/// <summary>
/// One diagram kind's side of the round trip, between the shared code editor
/// and that kind's canvas: it parses the code, shows the result, and turns
/// canvas edits into code edits. The shell (<see cref="Main"/>) reads the
/// diagram header, keeps the controller for that kind active and hands it the
/// code. See ADR-0001.
/// </summary>
public interface IDiagramController
{
	/// <summary>
	/// Raised with a title and a message when a canvas edit is refused, such
	/// as a rename to a name already taken, for the shell to show.
	/// </summary>
	event Action<string, string> ErrorReported;

	/// <summary>
	/// Shows the canvas and lets it take input, or hides it and stops it
	/// taking any, since a hidden canvas would still see every key and click.
	/// </summary>
	void SetActive(bool active);

	/// <summary>
	/// Parses <paramref name="body"/>, the code with its diagram header blanked
	/// out (<see cref="UMLDiagramHeader.RemoveFrom"/>), so line numbers still
	/// match the editor's. Shows the result on the canvas, or, if it has an
	/// error, marks the error in the code editor and grays out the canvas.
	/// </summary>
	void ShowCode(string body);

	/// <summary>
	/// Grays out the canvas, leaving the last diagram on it, for an error the
	/// shell found before this kind's parser ran, such as a bad header.
	/// </summary>
	void ShowInvalid();

	/// <summary>Fits the whole diagram on the canvas, as after opening a file.</summary>
	void FrameDiagram();
}
