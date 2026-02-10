DROP FUNCTION IF EXISTS map_bridge_pipeline;
CREATE FUNCTION map_bridge_pipeline (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Pipelines" AS
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
        "ProjectId",
        "Ref",
        "Sha",
        "Status",
        "CreatedAt",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        cast_to_nullable_bigint(_src->'project_id'),
        (_src->'ref')->>0,
        (_src->'sha')->>0,
        (_src->'status')->>0,
        date_gitlab_from_string((_src->'created_at')->>0),
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "ProjectId" = EXCLUDED."ProjectId",
        "Ref" = EXCLUDED."Ref",
        "Sha" = EXCLUDED."Sha",
        "Status" = EXCLUDED."Status",
        "CreatedAt" = EXCLUDED."CreatedAt",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Pipelines"."LastUpdatedByWebhookEventId")
    WHERE "Pipelines"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Pipelines" WHERE "Id" = v_id; 
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';