namespace Redpoint.CloudFramework.TypedRouting
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Provides helper functions for routing to actions in different controllers, since <c>nameof(MyController)</c>
    /// does not produce the correct value for the <c>controllerName</c> parameter.
    /// </summary>
    public static class TypedRoutingExtensions
    {
        private static string GetControllerName<T>()
        {
            var controllerName = typeof(T).Name;
            if (controllerName.EndsWith("Controller", StringComparison.Ordinal))
            {
                controllerName = controllerName.Substring(0, controllerName.Length - "Controller".Length);
            }
            return controllerName;
        }

        public static IActionResult RedirectToAction<T>(this Controller currentController, string actionName) where T : Controller
        {
            ArgumentNullException.ThrowIfNull(currentController);

            return currentController.RedirectToAction(actionName, GetControllerName<T>());
        }

        public static IActionResult RedirectToAction<T>(this Controller currentController, string actionName, object routeValues) where T : Controller
        {
            ArgumentNullException.ThrowIfNull(currentController);

            return currentController.RedirectToAction(actionName, GetControllerName<T>(), routeValues);
        }

        public static IActionResult RedirectToAction<T>(this Controller currentController, string actionName, object routeValues, string fragment) where T : Controller
        {
            ArgumentNullException.ThrowIfNull(currentController);

            return currentController.RedirectToAction(actionName, GetControllerName<T>(), routeValues, fragment);
        }

        public static string? Action<T>(this IUrlHelper urlHelper, string actionName) where T : Controller
        {
            return urlHelper.Action(actionName, GetControllerName<T>());
        }

        public static string? Action<T>(this IUrlHelper urlHelper, string actionName, object values) where T : Controller
        {
            return urlHelper.Action(actionName, GetControllerName<T>(), values);
        }

        public static string? Action<T>(this IUrlHelper urlHelper, string actionName, object values, string protocol) where T : Controller
        {
            return urlHelper.Action(actionName, GetControllerName<T>(), values, protocol);
        }

        public static string? Action<T>(this IUrlHelper urlHelper, string actionName, object values, string protocol, string host) where T : Controller
        {
            return urlHelper.Action(actionName, GetControllerName<T>(), values, protocol, host);
        }

        public static string? Action<T>(this IUrlHelper urlHelper, string actionName, object values, string protocol, string host, string fragment) where T : Controller
        {
            return urlHelper.Action(actionName, GetControllerName<T>(), values, protocol, host, fragment);
        }
    }
}
