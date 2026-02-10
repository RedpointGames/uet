export type Test = {
    id: string;
    fullName: string;
    status: string;
    startedUtcMillis: number | null;
    finishedUtcMillis: number | null;
}

export type PipelineBuild = {
    id: number;
    name: string;
    url: string;
    status: string;
    startedUtcMillis: number | null;
    estimatedUtcMillis: number | null;
    downstreamPipeline: Pipeline | null;
    tests: Test[];
}

export type PipelineStage = {
    name: string;
    builds: PipelineBuild[];
}

export type Pipeline = {
    id: number;
    title: string;
    url: string;
    stages: PipelineStage[]; 
    startedUtcMillis: number | null;
    estimatedUtcMillis: number | null;
    status: string;
}

export type BuildAnchors = {
    name: string;
    url: string;
    startedUtcMillis: number | null;
    estimatedUtcMillis: number | null;
}

export type RunnerBuild = {
    id: number;
    anchors: BuildAnchors[];
    status: string;
}

export type Runner = {
    id: number;
    description: string;
    builds: RunnerBuild[];
}

export type DashboardStats = {
    pendingPipelineCount: number;
    pendingBuildCount: number;
    runners: Runner[];
    pendingPipelines: Pipeline[];
}

export type HistoryStats = {
    recentPipelines: Pipeline[];
}

export type RunnerUtilizationStatsDatapoint = {
    timestampMinute: number;
    created: number;
    pending: number;
    running: number;
}

export type RunnerUtilizationStatsCapacityDistribution = {
    percentile: number;
    desiredCapacity: number;
}

export type RunnerUtilizationStats = {
    tag: string;
    capacity: number;
    datapoints: RunnerUtilizationStatsDatapoint[];
    desiredCapacity: number;
    desiredCapacityDistribution: RunnerUtilizationStatsCapacityDistribution[];
}

export type UtilizationStats = {
    runnerUtilizationStats: RunnerUtilizationStats[]
}

export type ProjectHealthStats = {
    projectId: number;
    name: string;
    webUrl: string;
    defaultBranch: string;
    pipelineId: number;
    status: string;
    sha: string;
}

export type HealthStats = {
    projectHealthStats: ProjectHealthStats[]
}