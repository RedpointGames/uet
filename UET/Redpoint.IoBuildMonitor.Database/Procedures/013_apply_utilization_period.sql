DROP FUNCTION IF EXISTS apply_utilization_period;
CREATE FUNCTION apply_utilization_period (
    IN _start TIMESTAMPTZ,
    IN _end TIMESTAMPTZ,
    IN _runner_id BIGINT) RETURNS VOID AS
$BODY$
DECLARE
    v_current TIMESTAMPTZ;
    v_current_week BIGINT;
    v_earliest_week BIGINT;
    v_start_minute BIGINT;
    v_start_point BIGINT;
    v_end_adjusted TIMESTAMPTZ;
    v_end_minute BIGINT;
    v_end_point BIGINT;
BEGIN
    v_current := CURRENT_TIMESTAMP;
    v_current_week := EXTRACT(WEEK FROM v_current) * EXTRACT(YEAR FROM v_current);

    v_earliest_week := v_current_week - 4;

    -- Backfill weeks for all runners to the earliest date where needed.
    INSERT INTO "UtilizationBlocks"
    SELECT 
        "W"."W" AS "Week", 
        "D"."D" AS "DayInWeek", 
        "H"."H" AS "HourQuarter",
        _runner_id AS "RunnerId",
        FALSE AS "InUse"
    FROM GENERATE_SERIES(v_earliest_week, v_current_week) AS "W"
    LEFT JOIN GENERATE_SERIES(0, 7 - 1) AS "D" ON 1 = 1
    LEFT JOIN GENERATE_SERIES(0, (24 * 15) - 1) AS "H" ON 1 = 1
    ON CONFLICT ("Week", "DayInWeek", "HourQuarter", "RunnerId") DO NOTHING;

    -- Compute the range that the runner was used.
    v_start_minute := EXTRACT(MINUTE FROM _start);
    v_start_point := 
        (EXTRACT(WEEK FROM _start) * EXTRACT(YEAR FROM _start) * (24 * 15 * 7)) +
        (EXTRACT(DOW FROM _start) * (24 * 15)) +
        (EXTRACT(HOUR FROM _start) * 15) +
        CASE
            WHEN v_start_minute >= 15 THEN 1
            WHEN v_start_minute >= 30 THEN 2
            WHEN v_start_minute >= 45 THEN 3
            ELSE 0
        END;
    v_end_adjusted := _end + INTERVAL '15 minutes';
    v_end_minute := EXTRACT(MINUTE FROM v_end_adjusted);
    v_end_point := 
        (EXTRACT(WEEK FROM v_end_adjusted) * EXTRACT(YEAR FROM v_end_adjusted) * (24 * 15 * 7)) +
        (EXTRACT(DOW FROM v_end_adjusted) * (24 * 15)) +
        (EXTRACT(HOUR FROM v_end_adjusted) * 15) +
        CASE
            WHEN v_end_minute >= 15 THEN 1
            WHEN v_end_minute >= 30 THEN 2
            WHEN v_end_minute >= 45 THEN 3
            ELSE 0
        END;

    -- Set InUse for the period that the runner was used.
    UPDATE "UtilizationBlocks"
    SET "InUse" = TRUE
    WHERE "RunnerId" = _runner_id AND (("Week" * (24 * 15 * 7)) + ("DayInWeek" * (24 * 15)) + "HourQuarter") BETWEEN v_start_point AND v_end_point;
END
$BODY$
LANGUAGE 'plpgsql';