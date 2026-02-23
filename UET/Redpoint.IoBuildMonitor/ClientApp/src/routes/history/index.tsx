import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { HistoryStatsContext } from "../../Contexts";
import { History } from "./components/History";

export function HistoryRoute() {
  return (
    <HistoryStatsContext.Consumer>
      {(historyStats) => {
        if (historyStats === undefined) {
          return <WaitingForConnection />;
        } else {
          return <History historyStats={historyStats} />;
        }
      }}
    </HistoryStatsContext.Consumer>
  );
}
