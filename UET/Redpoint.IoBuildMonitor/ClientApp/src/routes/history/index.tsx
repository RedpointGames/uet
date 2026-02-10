import React from "react";
import { Inject } from "react-injectable";
import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { HistoryStatsContext } from "../../Contexts";
import { HistoryStats } from "../../data/DataTypes";
import { History } from "./components/History";

interface InjectedProps {
  historyStats: HistoryStats | undefined;
}

export const HistoryRoute = Inject(
  {
    historyStats: HistoryStatsContext,
  },
  (props: InjectedProps) => {
    if (props.historyStats === undefined) {
      return (
        <WaitingForConnection />
      );
    } else {
      return <History historyStats={props.historyStats} />;
    }
  }
);
