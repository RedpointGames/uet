import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { UtilizationStatsContext } from "../../Contexts";
import { Utilization } from "./components/Utilization";

export function UtilizationRoute() {
  return (
    <UtilizationStatsContext.Consumer>
      {(utilizationStats) => {
        if (utilizationStats === undefined) {
          return <WaitingForConnection />;
        } else {
          return <Utilization utilizationStats={utilizationStats} />;
        }
      }}
    </UtilizationStatsContext.Consumer>
  );
}
