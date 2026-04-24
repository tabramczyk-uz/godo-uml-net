using Godot;
using System.Collections.Generic;

public class UMLDiagram : Resource
{
	public UMLDiagram()
	{
		Nodes = new List<UMLNode>();
		Relationships = new List<UMLRelationship>();
	}

	public List<UMLNode> Nodes { get; set; }
	public List<UMLRelationship> Relationships { get; set; }
}
