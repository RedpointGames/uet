import React from "react";
import { Inject } from "react-injectable";
import { WaitingForConnection } from "../../components/connection/WaitingForConnection";
import { DashboardStatsContext } from "../../Contexts";
import { DashboardStats } from "../../data/DataTypes";
import { Dashboard } from "./components/Dashboard";

interface InjectedProps {
    dashboardStats: DashboardStats | undefined;
}

export const DashboardRoute = Inject(
    {
        dashboardStats: DashboardStatsContext,
    },
    (props: InjectedProps) => {
        if (props.dashboardStats === undefined) {
            return (
                <WaitingForConnection />
            );
        } else {
            return <Dashboard dashboardStats={props.dashboardStats} />
        }
    });