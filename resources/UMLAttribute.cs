public class UMLAttribute
{
	public UMLAttribute(string name = "attribute", string type = "Integer", UMLParser.Visibility visibility = UMLParser.Visibility.Unknown)
	{
		Name = name;
		Type = type;
		Visibility = visibility;
	}

	public string Name { get; set; }
	public string Type { get; set; }
	public UMLParser.Visibility Visibility { get; set; }
}
