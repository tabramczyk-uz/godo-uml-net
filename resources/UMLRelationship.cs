public class UMLRelationship
{
	public UMLRelationship(UMLNode from, UMLNode to)
	{
		From = from;
		To = to;
	}

	public UMLNode From { get; set; }
	public UMLNode To { get; set; }
}
