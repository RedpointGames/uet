import {
  EuiHeader,
  EuiHeaderLink,
  EuiHeaderLinks,
  EuiHeaderLogo,
  EuiProvider,
} from "@elastic/eui";
import logo from "./logo.png";
import { matchPath, Route, Routes, useNavigate } from "react-router";
import { HealthWithFailureCount } from "./HealthWithFailureCount";
import { DashboardRoute } from "./routes/dashboard";
import { HealthRoute } from "./routes/health";
import { HistoryRoute } from "./routes/history";
import { NotFound } from "./routes/notFound";
import { UtilizationRoute } from "./routes/utilization";

export function App() {
  const navigate = useNavigate();

  return (
    <>
      <EuiProvider>
        <>
          <EuiHeader
            theme="dark"
            position="fixed"
            sections={[
              {
                items: [
                  <EuiHeaderLogo iconType={logo}>
                    Io Build Monitor
                  </EuiHeaderLogo>,
                  <EuiHeaderLinks aria-label="Io navigation links">
                    <EuiHeaderLink
                      isActive={
                        matchPath(
                          {
                            path: "/",
                            end: true,
                          },
                          location.pathname,
                        ) !== null
                      }
                      onClick={() => {
                        navigate("/");
                      }}
                    >
                      Dashboard
                    </EuiHeaderLink>
                    <HealthWithFailureCount />
                    <EuiHeaderLink
                      isActive={
                        matchPath(
                          {
                            path: "/history",
                            end: true,
                          },
                          location.pathname,
                        ) !== null
                      }
                      onClick={() => {
                        navigate("/history");
                      }}
                    >
                      History
                    </EuiHeaderLink>
                    <EuiHeaderLink
                      isActive={
                        matchPath(
                          {
                            path: "/utilization",
                            end: true,
                          },
                          location.pathname,
                        ) !== null
                      }
                      onClick={() => {
                        navigate("/utilization");
                      }}
                    >
                      Utilization
                    </EuiHeaderLink>
                  </EuiHeaderLinks>,
                ],
              },
            ]}
          />
          <Routes>
            <Route index Component={DashboardRoute} />
            <Route path="/health" Component={HealthRoute} />
            <Route path="/history" Component={HistoryRoute} />
            <Route path="/utilization" Component={UtilizationRoute} />
            <Route Component={NotFound} />
          </Routes>
        </>
      </EuiProvider>
    </>
  );
}
