using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using RAAMS.Application.Authorization;
using RAAMS.Application.Authorization.Attributes;
using RAAMS.Application.Common;

namespace RAAMS.API.Filters;

public class FieldMaskingFilter(ILogger<FieldMaskingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
            return;

        if (executed.Result is not ObjectResult { Value: not null } result ||
            context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        if (descriptor.MethodInfo.IsDefined(typeof(SkipFieldMaskingAttribute), inherit: true) ||
            descriptor.ControllerTypeInfo.IsDefined(typeof(SkipFieldMaskingAttribute), inherit: true))
            return;

        var maskable = descriptor.MethodInfo.GetCustomAttribute<MaskableAttribute>(inherit: true) ??
                       descriptor.ControllerTypeInfo.GetCustomAttribute<MaskableAttribute>(inherit: true);
        if (maskable is null)
            return;

        var roles = context.HttpContext.User.FindAll("role")
            .Concat(context.HttpContext.User.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var service = context.HttpContext.RequestServices.GetRequiredService<IFieldPermissionService>();
        var permissions = new Dictionary<string, FieldPermissionResult>(StringComparer.Ordinal);

        async Task MaskAsync(object item)
        {
            foreach (var property in item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var attribute = property.GetCustomAttribute<MaskablePropertyAttribute>(inherit: true);
                if (attribute is null)
                    continue;

                if (!permissions.TryGetValue(attribute.FieldName, out var permission))
                {
                    permission = await service.GetAsync(maskable.EntityType, attribute.FieldName, roles,
                        context.HttpContext.RequestAborted);
                    permissions.Add(attribute.FieldName, permission);
                }

                if (permission.CanView)
                    continue;

                if (property.SetMethod is not { IsPublic: true } || property.GetIndexParameters().Length != 0)
                    throw new InvalidOperationException($"Maskable property {item.GetType().FullName}.{property.Name} must have a public setter and no index parameters.");

                var propertyType = property.PropertyType;
                var maskedValue = propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null
                    ? Activator.CreateInstance(propertyType)
                    : null;
                property.SetValue(item, maskedValue);
                logger.LogDebug("Masked field {EntityType}.{FieldName}", maskable.EntityType, attribute.FieldName);
            }
        }

        if (result.Value is IPaginatedResult page)
        {
            foreach (var item in page.Items)
                if (item is not null)
                    await MaskAsync(item);
        }
        else if (result.Value is IEnumerable items and not string)
        {
            var maskedItems = new List<object?>();
            foreach (var item in items)
            {
                if (item is not null)
                    await MaskAsync(item);
                maskedItems.Add(item);
            }

            result.Value = maskedItems;
            result.DeclaredType = null;
        }
        else
        {
            await MaskAsync(result.Value);
        }
    }
}
