/// <summary>
/// One parameter of a <see cref="UMLMethod"/>. <see cref="Type"/> is empty when
/// the source code leaves it out.
/// </summary>
public class UMLMethodArgument
{
	public UMLMethodArgument(string name = "argument", string type = "")
	{
		Name = name;
		Type = type ?? "";
	}

	public string Name { get; set; }
	public string Type { get; set; }
}
