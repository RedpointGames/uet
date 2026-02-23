import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { HealthStatsContext } from "../../Contexts";
import { Health } from "./components/Health";

export function HealthRoute() {
  return (
    <HealthStatsContext.Consumer>
      {(healthStats) => {
        if (healthStats === undefined) {
          return <WaitingForConnection />;
        } else {
          return <Health healthStats={healthStats} />;
        }
      }}
    </HealthStatsContext.Consumer>
  );
}
