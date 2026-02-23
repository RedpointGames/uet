import { DateTime } from "luxon";
import React from "react";
import { type UseEuiTheme, withEuiTheme } from "@elastic/eui";

export const Countup = withEuiTheme(
  class Countup extends React.Component<{
    target: number | null;
    simple?: true;
    theme: UseEuiTheme<{}>;
  }> {
    private handle: any = null;

    constructor(props: {
      target: number | null;
      simple?: true;
      theme: UseEuiTheme<{}>;
    }) {
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

      let target = DateTime.fromMillis(this.props.target, { zone: "utc" });
      let remaining = target.diff(DateTime.utc());

      if (remaining.toMillis() > 0) {
        return <></>;
      }

      return (
        <span>
          {DateTime.utc()
            .plus(remaining)
            .toRelative({
              unit: ["days", "hours", "minutes", "seconds"],
              style: "short",
            })}
        </span>
      );
    }
  },
);
