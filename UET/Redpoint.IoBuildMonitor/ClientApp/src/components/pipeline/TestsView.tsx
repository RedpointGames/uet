/** 
 * @jsxRuntime classic
 * @jsx jsx 
 * @jsxFrag React.Fragment 
 **/

import { useEuiTheme, EuiTextColor } from "@elastic/eui";
import { faCircle, faDotCircle, faPlayCircle, faTimesCircle as faTimesCircleRegular } from "@fortawesome/free-regular-svg-icons";
import { faCheckCircle, faCircleNotch, faMinusCircle, faTimesCircle } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import React from "react";
import { css, jsx, SerializedStyles } from '@emotion/react';
import { ProgressGlob, ProgressNibblin } from "../common/Progress";
import { Pipeline } from "../../data/DataTypes";

interface PipelineIconInfo {
    anchor: React.ReactNode;
    id: number;
    tests: BuildIconInfo[];
}

interface BuildIconInfo {
    id: string;
    fullName: string;
    status: string;
    testInfo: React.ReactNode;
}

function emitBuildIcons(pipeline: Pipeline, anchors: string[]): PipelineIconInfo[] {
    const euiTheme = useEuiTheme().euiTheme;
    const iconCss = css`margin: 0.15rem`;
    const subduedText = css`color: ${euiTheme.colors.subduedText}`;

    let pipelines: PipelineIconInfo[] = [];

    let tests: BuildIconInfo[] = [];
    for (const stage of pipeline.stages) {
        for (const build of stage.builds) {
            for (const test of build.tests) {
                let testInfo = null;
                switch (test.status) {
                    case "listed":
                        testInfo = <EuiTextColor color="default"><FontAwesomeIcon title={build.name} icon={faCircle} css={iconCss} /></EuiTextColor>;
                        break;
                    case "running":
                        testInfo = <FontAwesomeIcon title={test.fullName} icon={faCircleNotch} css={iconCss} spin />;
                        break;
                    case "failed":
                        testInfo = <EuiTextColor color="danger"><FontAwesomeIcon title={test.fullName} icon={faTimesCircle} css={iconCss} /></EuiTextColor>;
                        break;
                    case "passed":
                        testInfo = <EuiTextColor color="success"><FontAwesomeIcon title={test.fullName} icon={faCheckCircle} css={iconCss} /></EuiTextColor>;
                        break;
                    default:
                        console.log(`Unknown test status: ${test.status}`);
                        break;
                }
                tests.push({
                    id: test.id,
                    fullName: test.fullName,
                    status: test.status,
                    testInfo
                });
            }
            if (build.downstreamPipeline !== null) {
                pipelines = [...pipelines, ...emitBuildIcons(build.downstreamPipeline, [...anchors, build.name])];
            }
        }
    }

    let anchorSpans = [];
    for (const anchor of anchors) {
        if (anchorSpans.length !== 0) {
            anchorSpans.push(<span key={anchorSpans.length} css={subduedText}>&nbsp; 🡒 &nbsp;</span>);
        }
        anchorSpans.push(<span key={anchorSpans.length}>{anchor}</span>);
    }

    if (tests.length > 0) {
        tests.sort((a, b) => {
            return b.fullName.localeCompare(a.fullName);
        });

        pipelines.push({
            anchor: <>{anchorSpans}</>,
            id: pipeline.id,
            tests,
        })
    }

    return pipelines;
}

export function TestsView(props: { pipeline: Pipeline }) {
    const euiTheme = useEuiTheme().euiTheme;

    const testFirstPipelineCss = css`
display: block;
font-size: 12px;
line-height: 1em;
padding-left: 1px;
margin: 2px 0 0.625rem 0;
`;
    const testPipelineCss = css`
display: block;
font-size: 12px;
line-height: 1em;
padding-left: 1px;
margin: calc(0.625rem + 2px) 0 0.625rem 0;
`;
    const testEntryCss = css`
line-height: 0;
display: inline-block;
`;
    const testTopLevelCss = css`
vertical-align: top;
display: inline-block;
top: 3px;
position: relative;
margin-bottom: 4px;
line-height: 0;
`;
    
    const runningTestEntryCss = css`
width: 100%; 
margin-bottom: 4px;
`;
    const runningTestTopLevelCss = css`
vertical-align: top;
display: inline-block;
position: relative;
width: 100%;
`;
    
    const iconMarginRight = euiTheme.size.s;

    let runningTests = []
    let pipelines = emitBuildIcons(props.pipeline, []);
    for (let pipeline of pipelines) {
        for (let test of pipeline.tests) {
            if (test.status === "running") {
                runningTests.push(
                    <div key={test.id} css={runningTestEntryCss}>
                        <ProgressGlob type="indeterminate" />
                        <FontAwesomeIcon css={css`margin-right: ${iconMarginRight}; color: ${euiTheme.colors.primary};`} icon={faCircleNotch} spin fixedWidth />
                        {test.fullName}
                    </div>
                )
            }
        }
    }

    let tests: React.ReactNode[] = [];
    for (let pipeline of pipelines) {
        tests.push(
            <div key={pipeline.id} css={tests.length === 0 ? testFirstPipelineCss : testPipelineCss}>
                {pipeline.anchor}
            </div>
        )
        for (let test of pipeline.tests) {
            tests.push(
                <div key={test.id} css={testEntryCss}>
                    <a target="_blank" className="text-dark">{test.testInfo}</a>
                </div>
            )
        }
    }
    
    return (
        <div css={css`width: 100%; flex-direction: row;`}>
            {runningTests.length > 0 ? (<div css={runningTestTopLevelCss}>
                {runningTests}
            </div>) : null}
            <div css={testTopLevelCss}>
                {tests}
            </div>
        </div>
    )
}