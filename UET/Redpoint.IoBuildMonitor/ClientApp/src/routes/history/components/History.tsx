import { EuiPageTemplate } from "@elastic/eui";
import React from "react";
import PipelineTable from "../../../components/pipeline/PipelineTable";
import { HistoryStats } from "../../../data/DataTypes";

export function History(props: { historyStats: HistoryStats }) {
  return (
    <EuiPageTemplate
      direction="row"
      restrictWidth={false}
      pageHeader={{
        iconType: "recentlyViewedApp",
        pageTitle: "History",
      }}
    >
      <PipelineTable
        pipelines={props.historyStats.recentPipelines}
        showFinished
      />
    </EuiPageTemplate>
  );
}
