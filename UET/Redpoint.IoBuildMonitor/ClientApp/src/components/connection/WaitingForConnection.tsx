import {
  EuiEmptyPrompt,
  EuiLoadingSpinner,
  EuiPageTemplate,
} from "@elastic/eui";
import { string } from "prop-types";
import React from "react";
import { Inject, InjectGuarded } from "react-injectable";
import { ConnectionStatusContext } from "../../Contexts";

export const WaitingForConnection = InjectGuarded({
    connection: ConnectionStatusContext,
}, (props: {
    connection: string,
}) => {
    return (
        <EuiPageTemplate direction="row" restrictWidth={false}>
            <EuiEmptyPrompt
                title={<h2>Connecting to Io</h2>}
                body={<><p>{props.connection}</p><EuiLoadingSpinner size="xxl" /></>}
            />
        </EuiPageTemplate>
    );
});
