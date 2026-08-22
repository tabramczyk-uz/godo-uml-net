/// <summary>
/// One entry of a classifier's attribute compartment. <see cref="Type"/> is
/// empty when the source code leaves it out, which is also how enumeration
/// literals are stored.
/// </summary>
public class UMLAttribute
{
	public UMLAttribute(
		string name = "attribute",
		string type = "",
		UMLVisibility visibility = UMLVisibility.Unknown
	)
	{
		Name = name;
		Type = type ?? "";
		Visibility = visibility;
	}

	public string Name { get; set; }
	public string Type { get; set; }
	public UMLVisibility Visibility { get; set; }
}
