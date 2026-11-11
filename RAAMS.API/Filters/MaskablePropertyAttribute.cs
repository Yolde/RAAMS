namespace RAAMS.API.Filters;

[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public class MaskablePropertyAttribute : Attribute
{
    public string FieldName { get; }

    public MaskablePropertyAttribute(string fieldName)
    {
        FieldName = fieldName;
    }
}
