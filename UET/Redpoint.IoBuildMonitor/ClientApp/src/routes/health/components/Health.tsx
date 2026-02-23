import React from "react";
import type { HealthStats } from "../../../data/DataTypes";
import {
  EuiCard,
  EuiCode,
  EuiFlexGrid,
  EuiFlexItem,
  EuiPageTemplate,
  EuiSpacer,
  EuiText,
  EuiTextColor,
  useEuiTheme,
} from "@elastic/eui";
import { css } from "@emotion/css";
import {
  faCircle,
  faTimesCircle,
  faCheckCircle,
  faPlayCircle,
  faCircleNotch,
  faDotCircle,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faTimesCircle as faTimesCircleRegular } from "@fortawesome/free-regular-svg-icons";
import { faGit } from "@fortawesome/free-brands-svg-icons";
import { useNavigate } from "react-router";

export const Health = (props: { healthStats: HealthStats }) => {
  const euiTheme = useEuiTheme().euiTheme;
  const navigate = useNavigate();

  const cards: React.ReactNode[] = [];
  const iconCss = css`
    margin-right: 0.25em;
  `;
  const iconPrimaryCss = css`
    margin-right: 0.25em;
    color: ${euiTheme.colors.primary};
  `;

  for (const row of props.healthStats.projectHealthStats) {
    const pipelineId = <EuiCode>#{row.pipelineId}</EuiCode>;
    let linkToDashboard = false;
    let buildInfo = (
      <>
        <FontAwesomeIcon fixedWidth icon={faCircle} className={iconCss} />{" "}
        {pipelineId}
      </>
    );
    switch (row.status) {
      case "failed":
        buildInfo = (
          <>
            <EuiTextColor color="danger">
              <FontAwesomeIcon
                fixedWidth
                icon={faTimesCircle}
                className={iconCss}
              />
            </EuiTextColor>{" "}
            Failed
          </>
        );
        break;
      case "success":
        buildInfo = (
          <>
            <EuiTextColor color="success">
              <FontAwesomeIcon
                fixedWidth
                icon={faCheckCircle}
                className={iconCss}
              />
            </EuiTextColor>{" "}
            Passed
          </>
        );
        break;
      case "manual":
        buildInfo = (
          <>
            <FontAwesomeIcon
              fixedWidth
              icon={faPlayCircle}
              className={iconCss}
            />{" "}
            Waiting for Manual Input
          </>
        );
        break;
      case "canceled":
        buildInfo = (
          <>
            <EuiTextColor color="default">
              <FontAwesomeIcon
                fixedWidth
                icon={faTimesCircleRegular}
                className={iconCss}
              />
            </EuiTextColor>{" "}
            Cancelled
          </>
        );
        break;
      case "pending":
      case "running":
        linkToDashboard = true;
        buildInfo = (
          <>
            <FontAwesomeIcon
              fixedWidth
              icon={faCircleNotch}
              className={iconPrimaryCss}
              spin
            />{" "}
            Running
          </>
        );
        break;
      case "queued":
        linkToDashboard = true;
        buildInfo = (
          <>
            <FontAwesomeIcon
              fixedWidth
              icon={faDotCircle}
              className={iconPrimaryCss}
            />{" "}
            Queued
          </>
        );
        break;
    }

    cards.push(
      <EuiFlexItem key={row.projectId}>
        <EuiCard
          title={
            <h4
              className={css`
                white-space: pre-line;
                overflow: hidden;
                text-overflow: ellipsis;
                display: -webkit-box;
                -webkit-line-clamp: 1;
                -webkit-box-orient: vertical;
              `}
            >
              {row.name}
            </h4>
          }
          titleElement="h4"
          titleSize="xs"
          textAlign="left"
          href={
            linkToDashboard
              ? undefined
              : `${row.webUrl}/-/pipelines/${row.pipelineId}`
          }
          onClick={
            !linkToDashboard
              ? undefined
              : () => {
                  if (linkToDashboard) {
                    navigate("/");
                  }
                }
          }
          target="_blank"
        >
          <EuiText>
            <FontAwesomeIcon
              fixedWidth
              icon={faGit}
              className={iconCss}
            ></FontAwesomeIcon>{" "}
            {row.sha.substring(0, 8)}
          </EuiText>
          <EuiSpacer size="xs" />
          <EuiText
            css={css`
              margin-bottom: -2px;
            `}
          >
            {buildInfo}
          </EuiText>
        </EuiCard>
      </EuiFlexItem>,
    );
  }

  return (
    <EuiPageTemplate direction="row" restrictWidth={false}>
      <EuiPageTemplate.Header
        iconType="monitoringApp"
        pageTitle="Project Health"
      />
      <EuiFlexGrid columns={3}>{cards}</EuiFlexGrid>
    </EuiPageTemplate>
  );
};
