/**
 * @jsxRuntime classic
 * @jsx jsx
 * @jsxFrag React.Fragment
 **/

import React, { Component } from "react";
import {
  matchPath,
  Route,
  Switch,
  useHistory,
  useLocation,
} from "react-router";
import {
  DashboardStats,
  HealthStats,
  HistoryStats,
  UtilizationStats,
} from "./data/DataTypes";
import * as signalR from "@microsoft/signalr";
import {
  ConnectionContext,
  ConnectionStatusContext,
  DashboardStatsContext,
  HealthStatsContext,
  HistoryStatsContext,
  UtilizationStatsContext,
} from "./Contexts";
import { DashboardRoute } from "./routes/dashboard";
import { NotFound } from "./routes/notFound";
import {
  EuiFacetButton,
  EuiHeader,
  EuiHeaderLink,
  EuiHeaderLinks,
  EuiHeaderLogo,
  EuiProvider,
  EuiThemeColorMode,
  useEuiBackgroundColor,
  useEuiTheme,
} from "@elastic/eui";
import { HistoryRoute } from "./routes/history";
import { Inject } from "react-injectable";
import { UtilizationRoute } from "./routes/utilization";
import { HealthRoute } from "./routes/health";
import { jsx } from "@emotion/react";

import "@elastic/eui/dist/eui_theme_dark.css";
import "@elastic/charts/dist/theme_only_dark.css";
import "./offset.scss";
import "./custom.css";

import logo from "./logo.png";

const HealthWithFailureCount = Inject(
  {
    healthStats: HealthStatsContext,
  },
  (props: { healthStats: HealthStats | undefined }) => {
    const location = useLocation();
    const history = useHistory();

    let failedCount = 0;
    if (props.healthStats !== undefined) {
      for (const project of props.healthStats.projectHealthStats) {
        if (project.status === "failed") {
          failedCount++;
        }
      }
    }

    if (failedCount === 0) {
      return (
        <EuiHeaderLink
          isActive={
            matchPath(location.pathname, { path: "/health", exact: true }) !==
            null
          }
          onClick={() => {
            history.push("/health");
          }}
        >
          Health
        </EuiHeaderLink>
      );
    } else {
      return (
        <EuiFacetButton quantity={failedCount} isSelected>
          <EuiHeaderLink
            isActive={
              matchPath(location.pathname, { path: "/health", exact: true }) !==
              null
            }
            onClick={() => {
              history.push("/health");
            }}
          >
            Health
          </EuiHeaderLink>
        </EuiFacetButton>
      );
    }
  }
);

function MainApp() {
  const location = useLocation();
  const history = useHistory();

  return (
    <EuiProvider colorMode={"dark"}>
      <EuiHeader
        theme="dark"
        position="fixed"
        sections={[
          {
            items: [
              <EuiHeaderLogo iconType={logo}>Io Build Monitor</EuiHeaderLogo>,
              <EuiHeaderLinks aria-label="Io navigation links">
                <EuiHeaderLink
                  isActive={
                    matchPath(location.pathname, { path: "/", exact: true }) !==
                    null
                  }
                  onClick={() => {
                    history.push("/");
                  }}
                >
                  Dashboard
                </EuiHeaderLink>
                <HealthWithFailureCount />
                <EuiHeaderLink
                  isActive={
                    matchPath(location.pathname, {
                      path: "/history",
                      exact: true,
                    }) !== null
                  }
                  onClick={() => {
                    history.push("/history");
                  }}
                >
                  History
                </EuiHeaderLink>
                <EuiHeaderLink
                  isActive={
                    matchPath(location.pathname, {
                      path: "/utilization",
                      exact: true,
                    }) !== null
                  }
                  onClick={() => {
                    history.push("/utilization");
                  }}
                >
                  Utilization
                </EuiHeaderLink>
              </EuiHeaderLinks>,
            ],
            borders: "right",
          },
        ]}
      />
      <Switch>
        <Route exact path="/" component={DashboardRoute} />
        <Route exact path="/health" component={HealthRoute} />
        <Route exact path="/history" component={HistoryRoute} />
        <Route exact path="/utilization" component={UtilizationRoute} />
        <Route component={NotFound} />
      </Switch>
    </EuiProvider>
  );
}

interface AppProps {}

interface AppState {
  dashboardStats?: DashboardStats;
  historyStats?: HistoryStats;
  utilizationStats?: UtilizationStats;
  healthStats?: HealthStats;
  connectionStatus: string;
}

export default class App extends Component<AppProps, AppState> {
  private connection: signalR.HubConnection | undefined;
  static displayName = "Io Build Monitor";

  constructor(props: AppProps) {
    super(props);
    this.state = {
      dashboardStats: undefined,
      historyStats: undefined,
      utilizationStats: undefined,
      healthStats: undefined,
      connectionStatus: "Establishing connection",
    };
  }

  public componentDidMount() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl("/hub")
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext: signalR.RetryContext) => {
          if (retryContext.previousRetryCount < 10) {
            return 100 * retryContext.previousRetryCount;
          }
          return 1000;
        },
      })
      .build();
    this.connection.onreconnecting(() => {
      this.setState({
        connectionStatus: "Connection lost; reconnecting",
      });
    });
    this.connection.on("DashboardUpdated", (dashboardStats: DashboardStats) => {
      this.setState({
        dashboardStats,
        connectionStatus: "Received dashboard data",
      });
    });
    this.connection.on("HealthUpdated", (healthStats: HealthStats) => {
      this.setState({
        healthStats,
        connectionStatus: "Received health data",
      });
    });
    this.connection.on("HistoryUpdated", (historyStats: HistoryStats) => {
      this.setState({
        historyStats,
        connectionStatus: "Received history data",
      });
    });
    this.connection.on(
      "UtilizationUpdated",
      (utilizationStats: UtilizationStats) => {
        this.setState({
          utilizationStats,
          connectionStatus: "Received utilization data",
        });
      }
    );
    this.connection.start().catch((err) => {
      console.error(err);
    });
  }

  public componentWillUnmount() {
    if (this.connection !== undefined) {
      this.connection.stop();
    }
    this.connection = undefined;
  }

  public render() {
    return (
      <ConnectionContext.Provider value={this.connection}>
        <ConnectionStatusContext.Provider value={this.state.connectionStatus}>
          <DashboardStatsContext.Provider value={this.state.dashboardStats}>
            <HistoryStatsContext.Provider value={this.state.historyStats}>
              <HealthStatsContext.Provider value={this.state.healthStats}>
                <UtilizationStatsContext.Provider
                  value={this.state.utilizationStats}
                >
                  <MainApp />
                </UtilizationStatsContext.Provider>
              </HealthStatsContext.Provider>
            </HistoryStatsContext.Provider>
          </DashboardStatsContext.Provider>
        </ConnectionStatusContext.Provider>
      </ConnectionContext.Provider>
    );
  }
}
