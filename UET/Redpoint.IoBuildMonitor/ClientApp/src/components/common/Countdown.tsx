/** 
 * @jsxRuntime classic
 * @jsx jsx 
 * @jsxFrag React.Fragment 
 **/

import { DateTime } from "luxon";
import React, { useEffect } from "react";
import { css, jsx } from '@emotion/react';
import { UseEuiTheme, withEuiTheme } from "@elastic/eui";

export const Countdown = withEuiTheme(class Countdown extends React.Component<{ target: number | null, simple?: true, theme: UseEuiTheme<{}> }> {
    private handle: any = null;
    
    constructor(props: { target: number | null, simple?: true, theme: UseEuiTheme<{}> }) {
        super(props);
    }

    public componentDidMount() {
        if (this.handle === null) {
            this.handle = setInterval(() => {
                this.forceUpdate();
            }, 100);
        }
    }

    public componentWillUnmount() {
        clearInterval(this.handle);
        this.handle = null;
    }

    public render() {
        if (this.props.target === null) {
            return <></>;
        }
    
        let target = DateTime.fromMillis(this.props.target, { zone: 'utc' });
        let remaining = target.diff(DateTime.utc());
    
        if (remaining.toMillis() < 0) {
            return <></>;
        }
    
        if (this.props.simple === true) {
            return <span css={css`color: ${this.props.theme.euiTheme.colors.subduedText}`}>{DateTime.utc().plus(remaining).toRelative({ unit: ["minutes", "seconds"], style: 'short' })?.substring(3).replace(/\./g, '')}</span>
        }

        return <span css={css`color: ${this.props.theme.euiTheme.colors.subduedText}; margin-left: 0.36rem;`}>({DateTime.utc().plus(remaining).toRelative({ unit: ["minutes", "seconds"], style: 'short' })?.substring(3).replace(/\./g, '')})</span>
    }
});