import React from "react";
import { DashboardStats } from "../../../data/DataTypes";
import {
  EuiFlexGroup,
  EuiFlexItem,
  EuiPageTemplate,
  EuiPanel,
  EuiSpacer,
  EuiStat,
  useEuiTheme,
} from "@elastic/eui";
import RunnerTable from "./RunnerTable";
import PipelineTable from "../../../components/pipeline/PipelineTable";

export function Dashboard(props: { dashboardStats: DashboardStats }) {
  const theme = useEuiTheme();
  return (
    <EuiPageTemplate
      direction="row"
      restrictWidth={false}
      pageHeader={{
        iconType: "dashboardApp",
        pageTitle: "Dashboard",
        color: theme.colorMode
      }}
    >
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
