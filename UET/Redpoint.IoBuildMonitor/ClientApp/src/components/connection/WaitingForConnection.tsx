import {
  EuiEmptyPrompt,
  EuiLoadingSpinner,
  EuiPageTemplate,
} from "@elastic/eui";
import { ConnectionStatusContext } from "../../Contexts";

export function WaitingForConnection() {
  return (
    <ConnectionStatusContext.Consumer>
      {(connection) => (
        <EuiPageTemplate direction="row" restrictWidth={false}>
          <EuiEmptyPrompt
            title={<h2>Connecting to Io</h2>}
            body={
              <>
                <p>{connection}</p>
                <EuiLoadingSpinner size="xxl" />
              </>
            }
          />
        </EuiPageTemplate>
      )}
    </ConnectionStatusContext.Consumer>
  );
}
