/** 
 * @jsxRuntime classic
 * @jsx jsx 
 * @jsxFrag React.Fragment 
 **/

import { UseEuiTheme, useEuiTheme, withEuiTheme } from "@elastic/eui";
import { faCircleNotch } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { DateTime } from "luxon";
import React from "react";
import { css, jsx } from '@emotion/react';
import { EuiProgress } from "@elastic/eui";

const progressBarStyle = css`
border-radius: 3px;
height: 3px;
`;
const progressNibblinStyle = css`
border-radius: 0.5em;
height: 1em;
width: 1em;
vertical-align: -0.125em;
margin: 0.15rem;
display: inline-block;
`;

export function ProgressGlob(props: { type: "indeterminate" | "success" | "failed" | "created" | "stopped" } | { type: "inprogress", start: number, estimate: number }) {
    const euiTheme = useEuiTheme().euiTheme;
    const cssStyle = css`margin-bottom: ${euiTheme.size.xs}; margin-top: ${euiTheme.size.xxs};`;

    if (props.type === 'inprogress') {
        return (
            <div css={cssStyle}>
                <Progress start={props.start} estimate={props.estimate} />
            </div>
        );
    } else if (props.type === 'success') {
        return (
            <div css={cssStyle}>
                <EuiProgress css={progressBarStyle} color="success" value={100} max={100} />
            </div>
        );
    } else if (props.type === 'failed') {
        return (
            <div css={cssStyle}>
                <EuiProgress css={progressBarStyle} color="danger" value={100} max={100} />
            </div>
        );
    } else if (props.type === 'created') {
        return (
            <div css={cssStyle}>
                <EuiProgress css={progressBarStyle} color="warning" value={100} max={100} />
            </div>
        );
    } else if (props.type === 'indeterminate') {
        return (
            <div css={cssStyle}>
                <EuiProgress css={progressBarStyle} color="primary" />
            </div>
        );
    } else {
        return (
            <div css={cssStyle}>
                <EuiProgress css={progressBarStyle} color="subdued" value={0} max={100} />
            </div>
        );
    };
}

export class Progress extends React.Component<{ start: number; estimate: number; }> {
    private painting: boolean = false;

    constructor(props: { start: number; estimate: number; }) {
        super(props);
    }

    private handlePaint = () => {
        if (this.painting) {
            this.forceUpdate();
            window.requestAnimationFrame(this.handlePaint);
        }
    }

    public componentDidMount() {
        this.painting = true;
        window.requestAnimationFrame(this.handlePaint);
    }

    public componentWillUnmount() {
        this.painting = false;
    }

    public render() {
        let currentMillis = DateTime.utc().toMillis();
        currentMillis -= this.props.start;
        let endMillis = this.props.estimate - this.props.start;
        if (endMillis > 0) {
            let progressAmount = (currentMillis / endMillis) * 100;
            if (progressAmount <= 0) { progressAmount = 0; }
            if (progressAmount >= 100) {
                progressAmount = 100;
                return (
                    <EuiProgress css={progressBarStyle} color="primary" />
                );
            }
            return (
                <EuiProgress css={progressBarStyle} color="primary" value={progressAmount} max={100} />
            );
        }
        return (
            <EuiProgress css={progressBarStyle} color="primary" />
        );
    }
}

interface ProgressNibblinProps {
    start: number; 
    estimate: number; 
    buildName: string;
    theme: UseEuiTheme<{}>;
}

export const ProgressNibblin = withEuiTheme(class ProgressNibblinImpl extends React.Component<ProgressNibblinProps> {
    private painting: boolean = false;

    constructor(props: ProgressNibblinProps) {
        super(props);
    }

    private handlePaint = () => {
        if (this.painting) {
            this.forceUpdate();
            window.requestAnimationFrame(this.handlePaint);
        }
    }

    public componentDidMount() {
        this.painting = true;
        window.requestAnimationFrame(this.handlePaint);
    }

    public componentWillUnmount() {
        this.painting = false;
    }

    public render() {
        let spinnerCss = css`margin: 0.15rem; color: ${this.props.theme.euiTheme.colors.primary}`;

        let currentMillis = DateTime.utc().toMillis();
        currentMillis -= this.props.start;
        let endMillis = this.props.estimate - this.props.start;
        if (endMillis > 0) {
            let extraClasses = " progress-bar-striped progress-bar-animated";
            let progressAmount = (currentMillis / endMillis) * 100;
            if (progressAmount <= 0) { progressAmount = 0; }
            if (progressAmount >= 100) {
                return (
                    <FontAwesomeIcon title={this.props.buildName} icon={faCircleNotch} css={spinnerCss} spin />
                );
            } else {
                let width = progressAmount;
                if (width < 100 / 16) {
                    width = 100 / 16;
                }
                return (
                    <EuiProgress css={progressNibblinStyle} color="primary" value={progressAmount} max={100} />
                );

                /*
                

                    <div style={{ display: 'inline-block', verticalAlign: '-0.125em', margin: '0.15rem', height: '1em', width: '1em' }}>
                        <div className="progress" style={{ borderRadius: '0.5rem' }} title={this.props.buildName + ' (' + Math.round(progressAmount) + '%)'}>
                            <div className={"progress-bar" + extraClasses} role="progressbar" style={{ width: width + '%' }} aria-valuenow={progressAmount} aria-valuemin={0} aria-valuemax={100}></div>
                        </div>
                    </div>
                    */
            }
        } else {
            return (
                <FontAwesomeIcon title={this.props.buildName} icon={faCircleNotch} css={spinnerCss} spin />
            );
        }
    }
});