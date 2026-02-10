DROP FUNCTION IF EXISTS map_build_webhook;
CREATE FUNCTION map_build_webhook (IN _src jsonb, IN _webhook_event_id BIGINT, IN _webhook_received_at TIMESTAMPTZ) RETURNS SETOF "Builds" AS
$BODY$
DECLARE 
    v_id BIGINT;
    v_build_raw JSONB;
    v_build "Builds"%ROWTYPE;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'build_id') = 'null' OR jsonb_typeof(_src->'pipeline_id') = 'null' OR jsonb_typeof(_src->'project_id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'build_id')::BIGINT;

    -- Construct a JSON map that is compatible with map_build.
    v_build_raw := jsonb_build_object(
        'id',               _src->'build_id',
        'name',             _src->'build_name',
        'stage',            _src->'build_stage',
        'status',           _src->'build_status',
        'created_at',       _src->'build_created_at',
        'started_at',       _src->'build_started_at',
        'finished_at',      _src->'build_finished_at',
        'duration',         _src->'build_duration',
        'allow_failure',    _src->'build_allow_failure',
        'user',             _src->'user',
        'runner',           _src->'runner'
    );

    -- Map the build.
    v_build := map_build(v_build_raw, _webhook_event_id, _webhook_received_at, NULL::BIGINT);
    IF v_build IS NULL THEN
        RETURN;
    END IF;

    -- Create a pipeline if needed and backfill.
    IF v_build."PipelineId" IS NULL AND jsonb_typeof(_src->'pipeline_id') <> 'null' AND jsonb_typeof(_src->'project_id') <> 'null' THEN
        INSERT INTO "Projects"
        (
            "Id"
        )
        VALUES
        (
            (_src->'project_id')::BIGINT
        ) ON CONFLICT ("Id") DO NOTHING;
        INSERT INTO "Pipelines"
        (
            "Id",
            "ProjectId"
        )
        VALUES
        (
            (_src->'pipeline_id')::BIGINT,
            (_src->'project_id')::BIGINT
        ) ON CONFLICT ("Id") DO NOTHING;
        UPDATE "Builds"
        SET "PipelineId" = (_src->'pipeline_id')::BIGINT
        WHERE "Id" = (_src->'build_id')::BIGINT;
    END IF;

    -- Refresh the build row.
    SELECT * INTO v_build FROM "Builds" WHERE "Id" = v_id;
    
    RETURN NEXT v_build;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';