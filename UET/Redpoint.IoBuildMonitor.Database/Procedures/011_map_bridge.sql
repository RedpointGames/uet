DROP FUNCTION IF EXISTS map_bridge;
CREATE FUNCTION map_bridge (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Builds" AS
$BODY$
DECLARE 
    existing_row "Builds"%ROWTYPE;
    selected_count INT;
    v_id BIGINT;
    v_intended_pipeline "Pipelines"%ROWTYPE;
    v_intended_downstream_pipeline "Pipelines"%ROWTYPE;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    SELECT * INTO existing_row FROM "Builds" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;

    v_intended_pipeline := map_bridge_pipeline(_src->'pipeline', _webhook_event_id);
    v_intended_downstream_pipeline := map_bridge_pipeline(_src->'downstream_pipeline', _webhook_event_id);
    
    INSERT INTO "Builds"
    (
        "Id",
        "Stage",
        "Name",
        "Status",
        "PipelineId",
        "DownstreamPipelineId",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'stage')->>0,
        (_src->'name')->>0,
        (_src->'status')->>0,
        v_intended_pipeline."Id",
        v_intended_downstream_pipeline."Id",
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Stage" = EXCLUDED."Stage",
        "Name" = EXCLUDED."Name",
        "Status" = EXCLUDED."Status",
        "PipelineId" = EXCLUDED."PipelineId",
        "DownstreamPipelineId" = EXCLUDED."DownstreamPipelineId",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Builds"."LastUpdatedByWebhookEventId")
    WHERE "Builds"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Builds" WHERE "Id" = v_id; 

    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';