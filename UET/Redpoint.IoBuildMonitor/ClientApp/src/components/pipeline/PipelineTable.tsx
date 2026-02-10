import { EuiBasicTable, EuiSpacer, useEuiTheme } from "@elastic/eui";
import React from "react";
import { Pipeline } from "../../data/DataTypes";
import { Countdown } from "../common/Countdown";
import { Countup } from "../common/Countup";
import { PipelineView } from "./PipelineView";
import { TestsView } from "./TestsView";

function PipelineDescriptionRenderer(props: { pipeline: Pipeline }) {
    let countdown = null;
    if (props.pipeline.startedUtcMillis !== null && props.pipeline.estimatedUtcMillis !== null) {
        countdown = <>
            <br />
            <Countdown target={props.pipeline.estimatedUtcMillis} simple />
        </>   
    }

    return <>
        <a style={{whiteSpace: 'nowrap'}} href={props.pipeline.url} target="_blank">{props.pipeline.title}</a>
        {countdown}
    </>
}

function PipelineFinishedRenderer(props: { pipeline: Pipeline }) {
    let countup = null;
    if (props.pipeline.estimatedUtcMillis !== null) {
        countup = <Countup target={props.pipeline.estimatedUtcMillis} />;
    }

    return <>{countup}</>
}

export default function PipelineTable(props: { pipelines: Pipeline[], showFinished?: true }) {
    const theme = useEuiTheme();

    const columns: {
        field: string,
        name: string,
        valign: 'top',
        width?: string,
        render: (item: any) => React.ReactNode,
    }[] = [];
    columns.push(
        {
            field: 'pipeline',
            name: 'Pipeline',
            valign: 'top',
            render: (item: Pipeline) => <span style={{ whiteSpace: 'nowrap' }}><PipelineDescriptionRenderer pipeline={item} /></span>,
        }
    );
    if (props.showFinished) {
        columns.push(
            {
                field: 'finishedAt',
                name: 'Finished',
                valign: 'top',
                render: (item: Pipeline) => <span style={{ whiteSpace: 'nowrap' }}><PipelineFinishedRenderer pipeline={item} /></span>,
            }
        )
    }
    columns.push(
        {
            field: 'status',
            name: 'Status',
            width: '50%',
            valign: 'top',
            render: (item: Pipeline) => <div>
                <PipelineView pipeline={item} topLevel />
            </div>,
        }
    );
    columns.push(
        {
            field: 'tests',
            name: 'Tests',
            width: '50%',
            valign: 'top',
            render: (item: Pipeline) => <TestsView pipeline={item} />,
        }
    );

    const rows = [];
    for (const pipeline of props.pipelines) {
        rows.push({
            pipeline: pipeline,
            finishedAt: pipeline,
            status: pipeline,
            tests: pipeline,
        });
    }

    return (
        <EuiBasicTable<{ pipeline: Pipeline, status: Pipeline }>
            tableCaption="Active Pipelines"
            tableLayout="auto"
            items={rows}
            columns={columns}
            color={theme.colorMode}
            noItemsMessage="No pipelines are pending or in-progress." />
    );
}