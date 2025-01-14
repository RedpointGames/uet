/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Html;
using IHtmlString = Microsoft.AspNetCore.Html.IHtmlContent;
using Microsoft.AspNetCore.Mvc;

namespace React.AspNet
{
    /// <summary>
    /// HTML Helpers for utilising React from an ASP.NET MVC application.
    /// </summary>
    public static class HtmlHelperExtensions
    {
        [ThreadStatic]
        private static StringWriter _sharedStringWriter;

        /// <summary>
        /// Renders the specified React component
        /// </summary>
        /// <typeparam name="T">Type of the props</typeparam>
        /// <param name="htmlHelper">HTML helper</param>
        /// <param name="componentName">Name of the component</param>
        /// <param name="props">Props to initialise the component with</param>
        /// <param name="htmlTag">HTML tag to wrap the component in. Defaults to &lt;div&gt;</param>
        /// <param name="containerId">ID to use for the container HTML tag. Defaults to an auto-generated ID</param>
        /// <param name="clientOnly">Skip rendering server-side and only output client-side initialisation code. Defaults to <c>false</c></param>
        /// <param name="serverOnly">Skip rendering React specific data-attributes, container and client-side initialisation during server side rendering. Defaults to <c>false</c></param>
        /// <param name="containerClass">HTML class(es) to set on the container tag</param>
        /// <param name="exceptionHandler">A custom exception handler that will be called if a component throws during a render. Args: (Exception ex, string componentName, string containerId)</param>
        /// <param name="renderFunctions">Functions to call during component render</param>
        /// <returns>The component's HTML</returns>
        public static IHtmlString React<T>(
            this IHtmlHelper htmlHelper,
            string componentName,
            T props,
            string htmlTag = null,
            string containerId = null,
            bool clientOnly = false,
            bool serverOnly = false,
            string containerClass = null,
            Action<Exception, string, string> exceptionHandler = null,
            IRenderFunctions renderFunctions = null
        )
        {
            var instance = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<IReactEnvironment>();

            try
            {
                var reactComponent = instance.CreateComponent(componentName, props, containerId, clientOnly, serverOnly);
                if (!string.IsNullOrEmpty(htmlTag))
                {
                    reactComponent.ContainerTag = htmlTag;
                }

                if (!string.IsNullOrEmpty(containerClass))
                {
                    reactComponent.ContainerClass = containerClass;
                }

                return RenderToString(writer => reactComponent.RenderHtml(writer, clientOnly, serverOnly, exceptionHandler, renderFunctions));
            }
            finally
            {
                instance.ReturnEngineToPool();
            }
        }

        /// <summary>
        /// Renders the specified React component, along with its client-side initialisation code.
        /// Normally you would use the <see cref="React{T}"/> method, but <see cref="ReactWithInit{T}"/>
        /// is useful when rendering self-contained partial views.
        /// </summary>
        /// <typeparam name="T">Type of the props</typeparam>
        /// <param name="htmlHelper">HTML helper</param>
        /// <param name="componentName">Name of the component</param>
        /// <param name="props">Props to initialise the component with</param>
        /// <param name="htmlTag">HTML tag to wrap the component in. Defaults to &lt;div&gt;</param>
        /// <param name="containerId">ID to use for the container HTML tag. Defaults to an auto-generated ID</param>
        /// <param name="clientOnly">Skip rendering server-side and only output client-side initialisation code. Defaults to <c>false</c></param>
        /// <param name="serverOnly">Skip rendering React specific data-attributes, container and client-side initialisation during server side rendering. Defaults to <c>false</c></param>
        /// <param name="containerClass">HTML class(es) to set on the container tag</param>
        /// <param name="exceptionHandler">A custom exception handler that will be called if a component throws during a render. Args: (Exception ex, string componentName, string containerId)</param>
        /// <param name="renderFunctions">Functions to call during component render</param>
        /// <returns>The component's HTML</returns>
        public static IHtmlString ReactWithInit<T>(
            this IHtmlHelper htmlHelper,
            string componentName,
            T props,
            string htmlTag = null,
            string containerId = null,
            bool clientOnly = false,
            bool serverOnly = false,
            string containerClass = null,
            Action<Exception, string, string> exceptionHandler = null,
            IRenderFunctions renderFunctions = null
        )
        {
            var instance = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<IReactEnvironment>();

            try
            {
                var reactComponent = instance.CreateComponent(componentName, props, containerId, clientOnly);
                if (!string.IsNullOrEmpty(htmlTag))
                {
                    reactComponent.ContainerTag = htmlTag;
                }

                if (!string.IsNullOrEmpty(containerClass))
                {
                    reactComponent.ContainerClass = containerClass;
                }

                return RenderToString(writer =>
                {
                    reactComponent.RenderHtml(writer, clientOnly, serverOnly, exceptionHandler: exceptionHandler, renderFunctions);
                    writer.WriteLine();
                    WriteScriptTag(instance, writer, bodyWriter => reactComponent.RenderJavaScript(bodyWriter, waitForDOMContentLoad: true));
                });
            }
            finally
            {
                instance.ReturnEngineToPool();
            }
        }

        /// <summary>
        /// Renders the JavaScript required to initialise all components client-side. This will
        /// attach event handlers to the server-rendered HTML.
        /// </summary>
        /// <returns>JavaScript for all components</returns>
        public static IHtmlString ReactInitJavaScript(this IHtmlHelper htmlHelper, bool clientOnly = false)
        {
            var instance = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<IReactEnvironment>();

            try
            {
                return RenderToString(writer =>
                {
                    WriteScriptTag(instance, writer, bodyWriter => instance.GetInitJavaScript(bodyWriter, clientOnly));
                });
            }
            finally
            {
                instance.ReturnEngineToPool();
            }
        }

        /// <summary>
        /// Returns script tags based on the webpack asset manifest
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="urlHelper">Optional IUrlHelper instance. Enables the use of tilde/relative (~/) paths inside the expose-components.js file.</param>
        /// <returns></returns>
        public static IHtmlString ReactGetScriptPaths(this IHtmlHelper htmlHelper, IUrlHelper urlHelper = null)
        {
            var instance = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<IReactEnvironment>();

            string nonce = instance.Configuration.ScriptNonceProvider != null
                ? $" nonce=\"{instance.Configuration.ScriptNonceProvider()}\""
                : "";

            return new HtmlString(string.Join("", instance.GetScriptPaths()
                .Select(scriptPath => $"<script{nonce} src=\"{(urlHelper == null ? scriptPath : urlHelper.Content(scriptPath))}\"></script>")));
        }

        /// <summary>
        /// Returns style tags based on the webpack asset manifest
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="urlHelper">Optional IUrlHelper instance. Enables the use of tilde/relative (~/) paths inside the expose-components.js file.</param>
        /// <returns></returns>
        public static IHtmlString ReactGetStylePaths(this IHtmlHelper htmlHelper, IUrlHelper urlHelper = null)
        {
            var instance = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<IReactEnvironment>();

            return new HtmlString(string.Join("", instance.GetStylePaths()
                .Select(stylePath => $"<link rel=\"stylesheet\" href=\"{(urlHelper == null ? stylePath : urlHelper.Content(stylePath))}\" />")));
        }

        private static IHtmlString RenderToString(Action<StringWriter> withWriter)
        {
            var stringWriter = _sharedStringWriter;
            if (stringWriter != null)
            {
                stringWriter.GetStringBuilder().Clear();
            }
            else
            {
                _sharedStringWriter = stringWriter = new StringWriter(new StringBuilder(128));
            }

            withWriter(stringWriter);
            return new HtmlString(stringWriter.ToString());
        }

        private static void WriteScriptTag(IReactEnvironment instance, TextWriter writer, Action<TextWriter> bodyWriter)
        {
            writer.Write("<script");
            if (instance.Configuration.ScriptNonceProvider != null)
            {
                writer.Write(" nonce=\"");
                writer.Write(instance.Configuration.ScriptNonceProvider());
                writer.Write("\"");
            }

            writer.Write(">");

            bodyWriter(writer);

            writer.Write("</script>");
        }
    }
}
