DROP FUNCTION IF EXISTS map_pipeline;
CREATE FUNCTION map_pipeline (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Pipelines" AS
$BODY$
DECLARE 
    existing_row "Pipelines"%ROWTYPE;
    selected_count INT;
    v_id BIGINT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    SELECT * INTO existing_row FROM "Pipelines" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;

    INSERT INTO "Pipelines"
    (
        "Id",
        "Ref",
        "Tag",
        "Sha",
        "PreviousSha",
        "Source",
        "Status",
        "Stages",
        "CreatedAt",
        "FinishedAt",
        "Duration",
        "QueuedDuration",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'ref')->>0,
        cast_to_nullable_boolean(_src->'tag'),
        (_src->'sha')->>0,
        (_src->'before_sha')->>0,
        (_src->'source')->>0,
        (_src->'status')->>0,
        ARRAY(SELECT jsonb_array_elements_text(_src->'stages'))::TEXT[],
        date_gitlab_from_string((_src->'created_at')->>0),
        date_gitlab_from_string((_src->'finished_at')->>0),
        cast_to_nullable_bigint(_src->'duration'),
        cast_to_nullable_bigint(_src->'queued_duration'),
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Ref" = EXCLUDED."Ref",
        "Tag" = EXCLUDED."Tag",
        "Sha" = EXCLUDED."Sha",
        "PreviousSha" = EXCLUDED."PreviousSha",
        "Source" = EXCLUDED."Source",
        "Status" = EXCLUDED."Status",
        "Stages" = EXCLUDED."Stages",
        "CreatedAt" = EXCLUDED."CreatedAt",
        "FinishedAt" = EXCLUDED."FinishedAt",
        "Duration" = EXCLUDED."Duration",
        "QueuedDuration" = EXCLUDED."QueuedDuration",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Pipelines"."LastUpdatedByWebhookEventId")
    WHERE "Pipelines"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Pipelines" WHERE "Id" = v_id; 
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';