import { useEuiTheme, EuiTextColor } from "@elastic/eui";
import {
  faCircle,
  faDotCircle,
  faPlayCircle,
  faTimesCircle as faTimesCircleRegular,
} from "@fortawesome/free-regular-svg-icons";
import {
  faCheckCircle,
  faCircleNotch,
  faTimesCircle,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { css } from "@emotion/css";
import { ProgressNibblin } from "../common/Progress";
import type { Pipeline } from "../../data/DataTypes";

export function PipelineView(props: { pipeline: Pipeline; topLevel?: true }) {
  const euiTheme = useEuiTheme().euiTheme;

  const iconCss = css`
    margin: 0.15rem;
  `;
  const downstreamPipelineCss = css`
    border-left: solid #ccc 2px;
    display: inline-block;
    vertical-align: top;
    border-radius: 5px;
    margin-bottom: 4px;
    margin-left: 2px;
    padding-left: 2px;
    top: 0.0875rem;
    position: relative;
  `;
  const downstreamPipelineHolderCss = css`
    display: block;
    margin: 0.15rem;
    height: 1em;
  `;
  const downstreamPipelineSpanCss = css`
    line-height: 1em;
    color: ${euiTheme.colors.subduedText};
  `;
  const downstreamPipelineTitleCss = css`
    font-size: 12px;
    vertical-align: 0.125em;
  `;
  const pipelineEntryCss = css`
    line-height: 0;
  `;
  const pipelineViewCss = css`
    vertical-align: top;
    display: inline-block;
    white-space: nowrap;
  `;
  const pipelineViewTopLevelCss = css`
    vertical-align: top;
    display: inline-block;
    white-space: nowrap;
    top: 3px;
    position: relative;
    margin-bottom: 4px;
  `;

  let stages = [];
  for (const stage of props.pipeline.stages) {
    let builds = [];
    for (const build of stage.builds) {
      let buildInfo = (
        <FontAwesomeIcon
          title={build.name}
          icon={faCircle}
          className={iconCss}
        />
      );
      switch (build.status) {
        case "failed":
          buildInfo = (
            <EuiTextColor color="danger">
              <FontAwesomeIcon
                title={build.name}
                icon={faTimesCircle}
                className={iconCss}
              />
            </EuiTextColor>
          );
          break;
        case "success":
          buildInfo = (
            <EuiTextColor color="success">
              <FontAwesomeIcon
                title={build.name}
                icon={faCheckCircle}
                className={iconCss}
              />
            </EuiTextColor>
          );
          break;
        case "manual":
          buildInfo = (
            <FontAwesomeIcon
              title={build.name}
              icon={faPlayCircle}
              className={iconCss}
            />
          );
          break;
        case "created":
          buildInfo = (
            <EuiTextColor color="default">
              <FontAwesomeIcon
                title={build.name}
                icon={faCircle}
                className={iconCss}
              />
            </EuiTextColor>
          );
          break;
        case "skipped":
          buildInfo = (
            <EuiTextColor color="subdued">
              <FontAwesomeIcon
                title={build.name}
                icon={faCircle}
                className={iconCss}
              />
            </EuiTextColor>
          );
          break;
        case "canceled":
          buildInfo = (
            <EuiTextColor color="default">
              <FontAwesomeIcon
                title={build.name}
                icon={faTimesCircleRegular}
                className={iconCss}
              />
            </EuiTextColor>
          );
          break;
        case "pending":
        case "running":
          if (
            build.startedUtcMillis !== null &&
            build.estimatedUtcMillis !== null
          ) {
            buildInfo = (
              <ProgressNibblin
                buildName={build.name}
                start={build.startedUtcMillis}
                estimate={build.estimatedUtcMillis}
              />
            );
          } else {
            buildInfo = (
              <FontAwesomeIcon
                title={build.name}
                icon={faCircleNotch}
                className={iconCss}
                spin
              />
            );
          }
          break;
        case "queued":
          buildInfo = (
            <FontAwesomeIcon
              title={build.name}
              icon={faDotCircle}
              className={iconCss}
            />
          );
          break;
      }
      let downstreamPipeline = null;
      if (build.downstreamPipeline !== null) {
        downstreamPipeline = (
          <div className={downstreamPipelineCss}>
            <div className={downstreamPipelineHolderCss}>
              <span className={downstreamPipelineSpanCss}>
                <span className={downstreamPipelineTitleCss}>{build.name}</span>
              </span>
            </div>
            <PipelineView pipeline={build.downstreamPipeline} />
          </div>
        );
      }
      builds.push(
        <div key={build.id} className={pipelineEntryCss}>
          <a href={build.url} target="_blank" className="text-dark">
            {buildInfo}
          </a>
          {downstreamPipeline}
        </div>,
      );
    }

    stages.push(
      <div
        key={stage.name}
        className={props.topLevel ? pipelineViewTopLevelCss : pipelineViewCss}
      >
        {builds}
      </div>,
    );
  }
  return <>{stages}</>;
}
