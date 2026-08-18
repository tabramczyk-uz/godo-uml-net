using System.Collections.Generic;
using Godot;

public partial class UMLDiagram : Resource
{
	public UMLDiagram()
	{
		Nodes = [];
		Relationships = [];
	}

	public List<UMLNode> Nodes { get; set; }
	public List<UMLRelationship> Relationships { get; set; }
}
