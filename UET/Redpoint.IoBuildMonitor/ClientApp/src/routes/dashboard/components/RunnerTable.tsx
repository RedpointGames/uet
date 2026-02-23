import { EuiBasicTable, EuiSpacer, useEuiTheme } from "@elastic/eui";
import {
  faLinux,
  faWindows,
  faApple,
  faDocker,
} from "@fortawesome/free-brands-svg-icons";
import {
  faCheckCircle,
  faTimesCircle,
  faPauseCircle,
  faCircleNotch,
  faStopCircle,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import React, { Fragment } from "react";
import { Countdown } from "../../../components/common/Countdown";
import { ProgressGlob } from "../../../components/common/Progress";
import type {
  RunnerBuild,
  Runner,
  DashboardStats,
} from "../../../data/DataTypes";
import { css } from "@emotion/css";

function BuildsRenderer(props: { item: RunnerBuild[] }) {
  const euiTheme = useEuiTheme().euiTheme;

  const iconMarginRight = euiTheme.size.s;

  let buildInfos = [];
  let lastStartedMillis = null;
  let lastEstimatedMillis = null;
  for (const build of props.item) {
    let anchors = [];
    for (const anchor of build.anchors) {
      if (anchors.length !== 0) {
        anchors.push(
          <span
            key={"split" + anchors.length}
            className={css`
              color: ${euiTheme.colors.disabled};
            `}
          >
            &nbsp; 🡒 &nbsp;
          </span>,
        );
      }
      if (anchor.url === null) {
        anchors.push(
          <Fragment key={"elem" + anchors.length}>
            {anchor.name}
            <Countdown target={anchor.estimatedUtcMillis} />
          </Fragment>,
        );
      } else {
        anchors.push(
          <Fragment>
            <a key={"elem" + anchors.length} href={anchor.url} target="_blank">
              {anchor.name}
            </a>
            <Countdown target={anchor.estimatedUtcMillis} />
          </Fragment>,
        );
        lastStartedMillis = anchor.startedUtcMillis;
        lastEstimatedMillis = anchor.estimatedUtcMillis;
      }
    }

    if (buildInfos.length > 0) {
      buildInfos.push(<EuiSpacer key={build.id + "_spacer"} size="xs" />);
    }

    if (lastEstimatedMillis !== null && lastStartedMillis !== null) {
      buildInfos.push(
        <div
          key={build.id}
          className={css`
            width: 100%;
          `}
        >
          <ProgressGlob
            type="inprogress"
            start={lastStartedMillis}
            estimate={lastEstimatedMillis}
          />
          <FontAwesomeIcon
            className={css`
              margin-right: ${iconMarginRight};
              color: ${euiTheme.colors.primary};
            `}
            icon={faCircleNotch}
            spin
            fixedWidth
          />
          {anchors}
        </div>,
      );
    } else if (build.status === "success") {
      buildInfos.push(
        <div
          key={build.id}
          className={css`
            width: 100%;
          `}
        >
          <ProgressGlob type="success" />
          <FontAwesomeIcon
            className={css`
              margin-right: ${iconMarginRight};
              color: ${euiTheme.colors.success};
            `}
            icon={faCheckCircle}
            fixedWidth
          />
          {anchors}
        </div>,
      );
    } else if (build.status === "failed") {
      buildInfos.push(
        <div
          key={build.id}
          className={css`
            width: 100%;
          `}
        >
          <ProgressGlob type="failed" />
          <FontAwesomeIcon
            className={css`
              margin-right: ${iconMarginRight};
              color: ${euiTheme.colors.danger};
            `}
            icon={faTimesCircle}
            fixedWidth
          />
          {anchors}
        </div>,
      );
    } else if (build.status === "created") {
      buildInfos.push(
        <div
          key={build.id}
          className={css`
            width: 100%;
          `}
        >
          <ProgressGlob type="created" />
          <FontAwesomeIcon
            className={css`
              margin-right: ${iconMarginRight};
              color: ${euiTheme.colors.warning};
            `}
            icon={faPauseCircle}
            fixedWidth
          />
          {anchors}
        </div>,
      );
    } else if (build.status === "running") {
      buildInfos.push(
        <div
          key={build.id}
          className={css`
            width: 100%;
          `}
        >
          <ProgressGlob type="indeterminate" />
          <FontAwesomeIcon
            className={css`
              margin-right: ${iconMarginRight};
              color: ${euiTheme.colors.primary};
            `}
            icon={faCircleNotch}
            spin
            fixedWidth
          />
          {anchors}
        </div>,
      );
    }
  }
  if (buildInfos.length === 0) {
    buildInfos.push(
      <div
        key="stopped"
        className={css`
          width: 100%;
        `}
      >
        <ProgressGlob type="stopped" />
        <FontAwesomeIcon
          key="stopped"
          className={css`
            margin-right: ${iconMarginRight};
            color: ${euiTheme.colors.disabled};
          `}
          icon={faStopCircle}
          fixedWidth
        />
      </div>,
    );
  }
  return (
    <div
      className={css`
        width: 100%;
      `}
    >
      {buildInfos}
    </div>
  );
}

function RunnerDescriptionRenderer(props: { item: Runner }) {
  const runner = props.item;
  let description: React.ReactNode = runner.description;
  if (runner.description.endsWith("-linux")) {
    description = (
      <>
        <FontAwesomeIcon fixedWidth icon={faLinux} />
        &nbsp;
        {runner.description
          .substring(0, runner.description.length - "-linux".length)
          .toUpperCase()}
      </>
    );
  } else if (runner.description.endsWith("-windows")) {
    description = (
      <>
        <FontAwesomeIcon fixedWidth icon={faWindows} />
        &nbsp;
        {runner.description
          .substring(0, runner.description.length - "-windows".length)
          .toUpperCase()}
      </>
    );
  } else if (runner.description.endsWith("-mac")) {
    description = (
      <>
        <FontAwesomeIcon fixedWidth icon={faApple} />
        &nbsp;
        {runner.description
          .substring(0, runner.description.length - "-mac".length)
          .toUpperCase()}
      </>
    );
  } else if (runner.description.endsWith("-docker")) {
    description = (
      <>
        <FontAwesomeIcon fixedWidth icon={faDocker} />
        &nbsp;
        {runner.description
          .substring(0, runner.description.length - "-docker".length)
          .toUpperCase()}
      </>
    );
  }
  return <>{description}</>;
}

export default function RunnerTable(props: { dashboardStats: DashboardStats }) {
  const columns: {
    field: string;
    name: string;
    valign: "top";
    width?: string;
    render: (item: any) => React.ReactNode;
  }[] = [
    {
      field: "buildMachine",
      name: "Build Machine",
      valign: "top",
      render: (item: Runner) => (
        <span
          className={css`
            white-space: nowrap;
          `}
        >
          <RunnerDescriptionRenderer item={item} />
        </span>
      ),
    },
    {
      field: "status",
      name: "Working On...",
      valign: "top",
      width: "100%",
      render: (item: RunnerBuild[]) => (
        <>
          <BuildsRenderer item={item} />
        </>
      ),
    },
  ];

  const rows: { buildMachine: Runner; status: RunnerBuild[] }[] = [];
  for (const runner of props.dashboardStats.runners) {
    if (runner.builds.length > 0) {
      rows.push({
        buildMachine: runner,
        status: runner.builds,
      });
    }
  }

  return (
    <EuiBasicTable<{ buildMachine: Runner; status: RunnerBuild[] }>
      tableCaption="Runners"
      tableLayout="auto"
      items={rows}
      columns={columns}
      noItemsMessage="No runners have been seen, since no builds have been run."
    />
  );
}
