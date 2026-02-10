import React from "react";
import { Inject } from "react-injectable";
import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { UtilizationStatsContext } from "../../Contexts";
import { UtilizationStats } from "../../data/DataTypes";
import { Utilization } from "./components/Utilization";

interface InjectedProps {
  utilizationStats: UtilizationStats | undefined;
}

export const UtilizationRoute = Inject(
  {
    utilizationStats: UtilizationStatsContext,
  },
  (props: InjectedProps) => {
    if (props.utilizationStats === undefined) {
      return (
        <WaitingForConnection />
      );
    } else {
      return <Utilization utilizationStats={props.utilizationStats} />;
    }
  }
);
