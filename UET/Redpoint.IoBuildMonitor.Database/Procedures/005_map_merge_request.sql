DROP FUNCTION IF EXISTS map_merge_request;
CREATE FUNCTION map_merge_request (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "MergeRequests" AS
$BODY$
DECLARE 
    existing_row "MergeRequests"%ROWTYPE;
    selected_count INT;
    v_id BIGINT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' OR jsonb_typeof(_src->'iid') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    SELECT * INTO existing_row FROM "MergeRequests" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;

    INSERT INTO "MergeRequests"
    (
        "Id",
        "InternalId",
        "Title",
        "SourceBranch",
        "SourceProjectId",
        "TargetBranch",
        "TargetProjectId",
        "State",
        "MergeStatus",
        "Url",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'iid')::BIGINT,
        (_src->'title')->>0,
        (_src->'source_branch')->>0,
        cast_to_nullable_bigint(_src->'source_project_id'),
        (_src->'target_branch')->>0,
        cast_to_nullable_bigint(_src->'target_project_id'),
        (_src->'state')->>0,
        (_src->'merge_status')->>0,
        (_src->'url')->>0,
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "InternalId" = EXCLUDED."InternalId",
        "Title" = EXCLUDED."Title",
        "SourceBranch" = EXCLUDED."SourceBranch",
        "SourceProjectId" = EXCLUDED."SourceProjectId",
        "TargetBranch" = EXCLUDED."TargetBranch",
        "TargetProjectId" = EXCLUDED."TargetProjectId",
        "State" = EXCLUDED."State",
        "MergeStatus" = EXCLUDED."MergeStatus",
        "Url" = EXCLUDED."Url",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "MergeRequests"."LastUpdatedByWebhookEventId")
    WHERE "MergeRequests"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "MergeRequests" WHERE "Id" = v_id;
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';