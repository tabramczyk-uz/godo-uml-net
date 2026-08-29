using System.Collections.Generic;
using System.Text;

public interface IUMLProperty
{
    string Name { get; set; }
}

public static class IUMLPropertyExtensions {
  public static string ToListString(this IEnumerable<IUMLProperty> properties) {
			StringBuilder sb = new();

			foreach (IUMLProperty property in properties)
			{
				sb.AppendLine(property.Name);
			}

			return sb.ToString();
  }
}

