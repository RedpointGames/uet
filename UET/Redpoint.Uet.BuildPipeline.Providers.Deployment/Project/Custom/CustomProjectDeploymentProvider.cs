﻿namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class CustomProjectDeploymentProvider : IProjectDeploymentProvider
    {
        public string Type => "Custom";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentCustom;

        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> elements)
        {
            throw new NotImplementedException();
        }
    }
}
