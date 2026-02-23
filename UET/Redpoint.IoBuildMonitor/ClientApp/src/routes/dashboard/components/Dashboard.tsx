import { type DashboardStats } from "../../../data/DataTypes";
import {
  EuiFlexGroup,
  EuiFlexItem,
  EuiPageTemplate,
  EuiPanel,
  EuiSpacer,
  EuiStat,
} from "@elastic/eui";
import RunnerTable from "./RunnerTable";
import PipelineTable from "../../../components/pipeline/PipelineTable";

export function Dashboard(props: { dashboardStats: DashboardStats }) {
  return (
    <EuiPageTemplate direction="row" restrictWidth={false}>
      <EuiPageTemplate.Header iconType="dashboardApp" pageTitle="Dashboard" />
      <EuiFlexGroup>
        <EuiFlexItem>
          <EuiPanel>
            <EuiStat
              title={props.dashboardStats.pendingPipelineCount}
              description="Pending &amp; In-Progress Pipelines"
            />
          </EuiPanel>
        </EuiFlexItem>
        <EuiFlexItem>
          <EuiPanel>
            <EuiStat
              title={props.dashboardStats.pendingBuildCount}
              description="Pending &amp; In-Progress Builds"
            />
          </EuiPanel>
        </EuiFlexItem>
      </EuiFlexGroup>
      <EuiSpacer />
      <RunnerTable dashboardStats={props.dashboardStats} />
      <EuiSpacer />
      <PipelineTable pipelines={props.dashboardStats.pendingPipelines} />
    </EuiPageTemplate>
  );
}
