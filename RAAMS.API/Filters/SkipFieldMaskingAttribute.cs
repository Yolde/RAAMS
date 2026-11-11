namespace RAAMS.API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class SkipFieldMaskingAttribute : Attribute;
