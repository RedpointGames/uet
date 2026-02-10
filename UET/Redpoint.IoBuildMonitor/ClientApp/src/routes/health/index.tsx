import React from "react";
import { Inject } from "react-injectable";
import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { HealthStatsContext } from "../../Contexts";
import { HealthStats } from "../../data/DataTypes";
import { Health } from "./components/Health";

interface InjectedProps {
  healthStats: HealthStats | undefined;
}

export const HealthRoute = Inject(
  {
    healthStats: HealthStatsContext,
  },
  (props: InjectedProps) => {
    if (props.healthStats === undefined) {
      return (
        <WaitingForConnection />
      );
    } else {
      return <Health healthStats={props.healthStats} />;
    }
  }
);
