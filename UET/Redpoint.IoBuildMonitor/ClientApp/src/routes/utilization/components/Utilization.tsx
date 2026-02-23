import {
  AnnotationDomainType,
  AreaSeries,
  Axis,
  Chart,
  LineAnnotation,
  Settings,
} from "@elastic/charts";
import React, { useState } from "react";
import type { DashboardStats, UtilizationStats } from "../../../data/DataTypes";
import {
  EuiButton,
  EuiCallOut,
  EuiEmptyPrompt,
  EuiFlexGroup,
  EuiFlexItem,
  EuiIcon,
  EuiLoadingSpinner,
  EuiPageTemplate,
  EuiPanel,
  EuiSpacer,
  EuiStat,
  EuiText,
  EuiTitle,
} from "@elastic/eui";
import moment from "moment-timezone";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faApple,
  faDocker,
  faLinux,
  faWindows,
} from "@fortawesome/free-brands-svg-icons";
import { ConnectionContext, DashboardStatsContext } from "../../../Contexts";
import { css } from "@emotion/css";
import type { HubConnection } from "@microsoft/signalr";

export function Utilization(props: { utilizationStats: UtilizationStats }) {
  return (
    <ConnectionContext.Consumer>
      {(connection) =>
        connection == undefined ? null : (
          <DashboardStatsContext.Consumer>
            {(dashboardStats) =>
              dashboardStats == undefined ? null : (
                <UtilizationImpl
                  connection={connection}
                  dashboardStats={dashboardStats}
                  utilizationStats={props.utilizationStats}
                />
              )
            }
          </DashboardStatsContext.Consumer>
        )
      }
    </ConnectionContext.Consumer>
  );
}

const UtilizationImpl = (props: {
  connection: HubConnection;
  dashboardStats: DashboardStats;
  utilizationStats: UtilizationStats;
}) => {
  const [reprocessing, setReprocessing] = useState(false);

  const entries: React.ReactNode[] = [];
  let earliestTimestamp = null;

  for (const row of props.utilizationStats.runnerUtilizationStats) {
    let breakCount = 0;
    for (let i = 0; i < row.tag.length; i++) {
      if (row.tag[i] === "-") {
        breakCount++;
      }
    }
    if (breakCount >= 3) {
      continue;
    }

    let emittedAtLeastOne = false;
    for (const entry of row.datapoints) {
      if (entry.created + entry.pending + entry.running > 0) {
        emittedAtLeastOne = true;
        break;
      }
    }
    if (emittedAtLeastOne) {
      for (const entry of row.datapoints) {
        if (earliestTimestamp === null) {
          earliestTimestamp = entry.timestampMinute * 60;
        } else if (entry.timestampMinute * 60 < earliestTimestamp) {
          earliestTimestamp = entry.timestampMinute * 60;
        }
      }
    }
  }

  for (const row of props.utilizationStats.runnerUtilizationStats) {
    let breakCount = 0;
    for (let i = 0; i < row.tag.length; i++) {
      if (row.tag[i] === "-") {
        breakCount++;
      }
    }
    if (breakCount >= 3) {
      continue;
    }

    const percentileDistribution = [];
    for (const perc of row.desiredCapacityDistribution) {
      percentileDistribution.push([1 - perc.percentile, perc.desiredCapacity]);
    }

    const createdRows = [];
    const pendingRows = [];
    const runningRows = [];
    let emittedAtLeastOne = false;
    let emittedStartPoint = false;
    for (const entry of row.datapoints) {
      if (entry.created + entry.pending + entry.running > 0) {
        emittedAtLeastOne = true;
      }
      if (emittedAtLeastOne) {
        if (
          earliestTimestamp !== null &&
          entry.timestampMinute * 60 > earliestTimestamp &&
          !emittedStartPoint
        ) {
          createdRows.push([earliestTimestamp, 0]);
          pendingRows.push([earliestTimestamp, 0]);
          runningRows.push([earliestTimestamp, 0]);
          emittedStartPoint = true;

          if (entry.timestampMinute * 60 - 60 !== earliestTimestamp) {
            // Also add an entry right before the data so we don't get
            // a large gradient.
            createdRows.push([entry.timestampMinute * 60 - 60, 0]);
            pendingRows.push([entry.timestampMinute * 60 - 60, 0]);
            runningRows.push([entry.timestampMinute * 60 - 60, 0]);
          }
        }
        createdRows.push([entry.timestampMinute * 60, entry.created]);
        pendingRows.push([entry.timestampMinute * 60, entry.pending]);
        runningRows.push([entry.timestampMinute * 60, entry.running]);
      }
    }

    if (!emittedAtLeastOne) {
      continue;
    }

    let description: React.ReactNode = row.tag;
    if (row.tag.endsWith("-linux")) {
      description = (
        <>
          <FontAwesomeIcon fixedWidth icon={faLinux} />
          &nbsp;Linux
        </>
      );
    } else if (row.tag.endsWith("-windows")) {
      description = (
        <>
          <FontAwesomeIcon fixedWidth icon={faWindows} />
          &nbsp;Windows
        </>
      );
    } else if (row.tag.endsWith("-mac")) {
      description = (
        <>
          <FontAwesomeIcon fixedWidth icon={faApple} />
          &nbsp;macOS
        </>
      );
    } else if (row.tag.endsWith("-docker")) {
      description = (
        <>
          <FontAwesomeIcon fixedWidth icon={faDocker} />
          &nbsp;Linux (Docker)
        </>
      );
    }

    entries.push(
      <React.Fragment key={row.tag}>
        <EuiPanel>
          <EuiTitle size="s">
            <h2>{description}</h2>
          </EuiTitle>
          <EuiSpacer size="l" />
          <EuiFlexGroup direction="row">
            <EuiFlexItem>
              <Chart size={{ height: 200 }} key={row.tag}>
                <Settings showLegend={true} legendPosition="right" />
                <AreaSeries
                  id="running"
                  name="Running"
                  data={runningRows}
                  xAccessor={0}
                  yAccessors={[1]}
                  stackAccessors={[0]}
                />
                <AreaSeries
                  id="pending"
                  name="Pending"
                  data={pendingRows}
                  xAccessor={0}
                  yAccessors={[1]}
                  stackAccessors={[0]}
                />
                <AreaSeries
                  id="created"
                  name="Created"
                  data={createdRows}
                  xAccessor={0}
                  yAccessors={[1]}
                  stackAccessors={[0]}
                />
                <Axis
                  id="bottom-axis"
                  position="bottom"
                  tickFormat={(value: number) => {
                    return moment
                      .unix(value)
                      .tz("Australia/Melbourne")
                      .format("ddd HH:mm");
                  }}
                />
                <Axis
                  id="left-axis"
                  position="left"
                  tickFormat={(d) => Number(d).toFixed(0)}
                />
                <LineAnnotation
                  id="capacity"
                  domainType={AnnotationDomainType.YDomain}
                  dataValues={[
                    {
                      dataValue: row.capacity,
                    },
                  ]}
                />
              </Chart>
            </EuiFlexItem>
            <EuiFlexItem grow={false}>
              <EuiStat title={row.capacity} description="Capacity" />
              <EuiStat
                title={row.desiredCapacity}
                description="Desired Capacity (50% of the time)"
              />
              <Chart key={row.tag}>
                <Settings showLegend={false} />
                <AreaSeries
                  id="capacity"
                  name="Desired Capacity"
                  data={percentileDistribution}
                  xAccessor={0}
                  yAccessors={[1]}
                  stackAccessors={[0]}
                />
                <Axis
                  id="bottom-axis"
                  position="bottom"
                  tickFormat={(value: number) => {
                    return Math.round(100 - value * 100) + "% of the time";
                  }}
                  labelFormat={(value: number) => {
                    return Math.round(100 - value * 100) + "%";
                  }}
                  style={{
                    tickLabel: {
                      alignment: {
                        horizontal: "left",
                      },
                    },
                  }}
                />
                <Axis
                  id="left-axis"
                  position="right"
                  tickFormat={(d) => Number(d).toFixed(2)}
                />
              </Chart>
            </EuiFlexItem>
          </EuiFlexGroup>
        </EuiPanel>
        <EuiSpacer size="l" />
      </React.Fragment>,
    );
  }

  let callout: React.ReactNode = null;
  if (entries.length === 0) {
    entries.push(
      <EuiEmptyPrompt
        title={<h2>No Utilization Metrics Available</h2>}
        body={
          <>
            <p>
              There's no utilization metrics to display. Either no builds have
              been run in the last 24 hours, or Io is yet to process historical
              build data.
            </p>
            <p>
              This page will automatically update with utilization metrics once
              they're available.
            </p>
          </>
        }
      />,
    );
  } else if (props.dashboardStats.pendingBuildCount > 0) {
    callout = (
      <>
        <EuiCallOut
          title="Builds are running!"
          color="warning"
          iconType="alert"
        >
          <p>
            Utilization metrics will not show builds that are created or pending
            in the current build queue. This is because GitLab does not assign
            runner tags to a build until it starts running, so we don't know
            which graph to assign those builds to until the builds actually
            start.
          </p>
          <p>
            After the builds have started running, the utilization metrics
            processor will go back and fix up the historical data so that it is
            correct.
          </p>
        </EuiCallOut>
        <EuiSpacer size="l" />
      </>
    );
  }

  return (
    <EuiPageTemplate direction="row" restrictWidth={false}>
      <EuiPageTemplate.Header
        iconType="metricbeatApp"
        pageTitle="Utilization"
        rightSideItems={[
          <EuiButton
            disabled={reprocessing}
            onClick={async () => {
              setReprocessing(true);
              try {
                await props.connection.invoke("ResetUtilizationData");
              } finally {
                setReprocessing(false);
              }
            }}
          >
            {reprocessing ? (
              <>
                <EuiLoadingSpinner />
                &nbsp;
              </>
            ) : null}
            Re-process Utilization Data
          </EuiButton>,
        ]}
      />
      <EuiFlexGroup>
        <EuiFlexItem>
          <EuiText size="xs">
            <p>
              Utilization metrics show you how much your build agents are being
              used by builds over time. It is designed to help you decide when
              you need to invest in more build infrastructure.
            </p>
            <p>
              For this reason, utilization metrics exclude any build jobs that
              take less than 60 seconds to run. These quick build jobs are often
              constrained on job setup, rather than compute power in your build
              agent cluster.
            </p>
            <p>Builds are categorized as follows for utilization metrics:</p>
            <ul
              className={css`
                margin-inline-start: 0 !important;
                list-style: none !important;
              `}
            >
              <li>
                <EuiIcon
                  type="dot"
                  color="#9170B8"
                  css={css`
                    position: relative;
                    top: -1px;
                  `}
                />{" "}
                <strong>Created</strong>: These builds have been created on
                GitLab, but they can't start yet because they're missing
                dependencies or artifacts from other build jobs. These builds
                are <em>not considered</em> when determining desired capacity
                for build agents, because they can't start anyway.
              </li>
              <li>
                <EuiIcon
                  type="dot"
                  color="#6092C0"
                  css={css`
                    position: relative;
                    top: -1px;
                  `}
                />{" "}
                <strong>Pending</strong>: These builds would start, but they
                don't have a build agent to run on yet. This is most often
                because you don't have enough build agents for the amount of
                queued builds.
              </li>
              <li>
                <EuiIcon
                  type="dot"
                  color="#54B399"
                  css={css`
                    position: relative;
                    top: -1px;
                  `}
                />{" "}
                <strong>Running</strong>: These builds are running on a build
                agent.
              </li>
            </ul>
          </EuiText>
          <EuiSpacer size="l" />
          {callout}
          {entries}
        </EuiFlexItem>
      </EuiFlexGroup>
    </EuiPageTemplate>
  );
};
