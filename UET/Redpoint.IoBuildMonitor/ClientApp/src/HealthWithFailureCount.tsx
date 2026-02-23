import { HealthStatsContext } from "./Contexts";
import type { HealthStats } from "./data/DataTypes";
import { matchPath, useLocation, useNavigate } from "react-router";
import { EuiFacetButton, EuiHeaderLink } from "@elastic/eui";

export function HealthWithFailureCount() {
  return (
    <HealthStatsContext.Consumer>
      {(healthStats) => (
        <HealthWithFailureCountImpl healthStats={healthStats} />
      )}
    </HealthStatsContext.Consumer>
  );
}

function HealthWithFailureCountImpl(props: {
  healthStats: HealthStats | undefined;
}) {
  const location = useLocation();
  const navigate = useNavigate();

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
          matchPath({ path: "/health", end: true }, location.pathname) !== null
        }
        onClick={() => {
          navigate("/health");
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
            matchPath({ path: "/health", end: true }, location.pathname) !==
            null
          }
          onClick={() => {
            navigate("/health");
          }}
        >
          Health
        </EuiHeaderLink>
      </EuiFacetButton>
    );
  }
}
