namespace RAAMS.Application.Authorization.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class SkipFieldMaskingAttribute : Attribute;
