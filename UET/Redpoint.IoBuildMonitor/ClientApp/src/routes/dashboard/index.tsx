import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { DashboardStatsContext } from "../../Contexts";
import { Dashboard } from "./components/Dashboard";

export function DashboardRoute() {
  return (
    <DashboardStatsContext.Consumer>
      {(dashboardStats) => {
        if (dashboardStats === undefined) {
          return <WaitingForConnection />;
        } else {
          return <Dashboard dashboardStats={dashboardStats} />;
        }
      }}
    </DashboardStatsContext.Consumer>
  );
}
