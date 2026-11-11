namespace RAAMS.API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class MaskableAttribute : Attribute
{
    public string EntityType { get; }

    public MaskableAttribute(string entityType)
    {
        EntityType = entityType;
    }
}
