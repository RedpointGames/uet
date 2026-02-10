import React from "react";
import { DashboardStats, HealthStats, HistoryStats, UtilizationStats } from "./data/DataTypes";

export const ConnectionContext = React.createContext<signalR.HubConnection | undefined>(undefined);
export const ConnectionStatusContext = React.createContext<string | undefined>(undefined);
export const DashboardStatsContext = React.createContext<DashboardStats | undefined>(undefined);
export const HistoryStatsContext = React.createContext<HistoryStats | undefined>(undefined);
export const UtilizationStatsContext = React.createContext<UtilizationStats | undefined>(undefined);
export const HealthStatsContext = React.createContext<HealthStats | undefined>(undefined);