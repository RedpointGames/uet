DROP FUNCTION IF EXISTS map_runner;
CREATE FUNCTION map_runner (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Runners" AS
$BODY$
DECLARE 
    existing_row "Runners"%ROWTYPE;
    selected_count INT;
    v_id BIGINT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    SELECT * INTO existing_row FROM "Runners" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;
    
    INSERT INTO "Runners"
    (
        "Id",
        "Description",
        "Active",
        "RunnerType",
        "IsShared",
        "Tags",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'description')->>0,
        cast_to_nullable_boolean(_src->'active'),
        (_src->'runner_type')->>0,
        cast_to_nullable_boolean(_src->'is_shared'),
        ARRAY(SELECT jsonb_array_elements_text(_src->'tags'))::TEXT[],
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Description" = EXCLUDED."Description",
        "Active" = EXCLUDED."Active",
        "RunnerType" = EXCLUDED."RunnerType",
        "IsShared" = EXCLUDED."IsShared",
        "Tags" = EXCLUDED."Tags",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Runners"."LastUpdatedByWebhookEventId")
    WHERE "Runners"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Runners" WHERE "Id" = v_id;
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';