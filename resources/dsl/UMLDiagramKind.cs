/// <summary>
/// Which family of UML diagram a diagram is. It decides which parser, code
/// writer and canvas handle the diagram (see ADR-0001). The keyword that names
/// each kind in a diagram header lives in <see cref="UMLDiagramHeader"/>.
/// </summary>
public enum UMLDiagramKind
{
	/// <summary>Freely placed nodes joined by relationships; the default.</summary>
	Class,

	/// <summary>Participants left to right, messages top to bottom.</summary>
	Sequence,
}
