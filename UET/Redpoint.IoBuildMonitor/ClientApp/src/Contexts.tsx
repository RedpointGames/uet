import React from "react";
import type {
  DashboardStats,
  HealthStats,
  HistoryStats,
  UtilizationStats,
} from "./data/DataTypes";
import type { HubConnection } from "@microsoft/signalr";

export const ConnectionContext = React.createContext<HubConnection | undefined>(
  undefined,
);
export const ConnectionStatusContext = React.createContext<string | undefined>(
  undefined,
);
export const DashboardStatsContext = React.createContext<
  DashboardStats | undefined
>(undefined);
export const HistoryStatsContext = React.createContext<
  HistoryStats | undefined
>(undefined);
export const UtilizationStatsContext = React.createContext<
  UtilizationStats | undefined
>(undefined);
export const HealthStatsContext = React.createContext<HealthStats | undefined>(
  undefined,
);
