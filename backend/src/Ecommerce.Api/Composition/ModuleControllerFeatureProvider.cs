using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Ecommerce.Api.Composition;

// MVC discovers only public controllers by default. Module controllers are internal (module implementation
// types never leak), so this provider also accepts internal [ApiController] types from module assemblies.
internal sealed class ModuleControllerFeatureProvider(IReadOnlyCollection<Assembly> moduleAssemblies) : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        base.IsController(typeInfo) ||
        (moduleAssemblies.Contains(typeInfo.Assembly)
         && typeInfo is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false, IsNested: false }
         && typeof(ControllerBase).IsAssignableFrom(typeInfo)
         && typeInfo.IsDefined(typeof(ApiControllerAttribute), inherit: true));
}
