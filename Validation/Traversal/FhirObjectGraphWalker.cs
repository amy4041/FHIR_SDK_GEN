using System.Collections;
using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.Validation.Rules;

namespace MyFhirSdk.Validation.Traversal;

internal sealed class FhirObjectGraphWalker
{
    public IEnumerable<FhirObjectGraphNode> Walk(
        FhirObject root,
        ICollection<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(issues);

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return WalkObject(root, FhirPathFormatter.Root(root), issues, visited);
    }

    private static IEnumerable<FhirObjectGraphNode> WalkObject(
        FhirObject value,
        string path,
        ICollection<ValidationIssue> issues,
        ISet<object> visited)
    {
        if (!visited.Add(value))
        {
            yield break;
        }

        yield return new FhirObjectGraphNode(value, path);

        foreach (var property in GetReadableProperties(value.GetType()))
        {
            var propertyPath = FhirPathFormatter.Combine(path, FhirPathFormatter.PropertyName(property));
            var propertyValue = property.GetValue(value);

            if (IsRepeatedProperty(property))
            {
                foreach (var child in WalkRepeatedProperty(propertyValue, propertyPath, issues, visited))
                {
                    yield return child;
                }

                continue;
            }

            if (propertyValue is FhirObject childObject)
            {
                foreach (var child in WalkObject(childObject, propertyPath, issues, visited))
                {
                    yield return child;
                }
            }
        }
    }

    private static IEnumerable<FhirObjectGraphNode> WalkRepeatedProperty(
        object? propertyValue,
        string propertyPath,
        ICollection<ValidationIssue> issues,
        ISet<object> visited)
    {
        if (propertyValue is null)
        {
            CardinalityRule.AddNullListIssue(propertyPath, issues);
            yield break;
        }

        if (propertyValue is not IEnumerable values)
        {
            yield break;
        }

        var index = 0;
        foreach (var item in values)
        {
            var itemPath = FhirPathFormatter.Indexed(propertyPath, index);

            if (item is null)
            {
                CardinalityRule.AddNullItemIssue(itemPath, issues);
            }
            else if (item is FhirObject childObject)
            {
                foreach (var child in WalkObject(childObject, itemPath, issues, visited))
                {
                    yield return child;
                }
            }

            index++;
        }
    }

    private static IEnumerable<PropertyInfo> GetReadableProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);
    }

    private static bool IsRepeatedProperty(PropertyInfo property)
    {
        return property.PropertyType != typeof(string)
            && typeof(IEnumerable).IsAssignableFrom(property.PropertyType);
    }
}
