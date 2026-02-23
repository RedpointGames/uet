import { EuiPageTemplate } from "@elastic/eui";
import PipelineTable from "../../../components/pipeline/PipelineTable";
import type { HistoryStats } from "../../../data/DataTypes";

export function History(props: { historyStats: HistoryStats }) {
  return (
    <EuiPageTemplate direction="row" restrictWidth={false}>
      <EuiPageTemplate.Header
        iconType="recentlyViewedApp"
        pageTitle="History"
      />
      <PipelineTable
        pipelines={props.historyStats.recentPipelines}
        showFinished
      />
    </EuiPageTemplate>
  );
}
