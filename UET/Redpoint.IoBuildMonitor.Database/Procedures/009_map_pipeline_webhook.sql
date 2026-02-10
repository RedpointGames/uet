DROP FUNCTION IF EXISTS map_pipeline_webhook;
CREATE FUNCTION map_pipeline_webhook (IN _src jsonb, IN _webhook_event_id BIGINT, IN _webhook_received_at TIMESTAMPTZ) RETURNS SETOF "Pipelines" AS
$BODY$
DECLARE 
    v_pipeline "Pipelines"%ROWTYPE;
    v_merge_request "MergeRequests"%ROWTYPE;
    v_user "Users"%ROWTYPE;
    v_project "Projects"%ROWTYPE;
    v_commit "Commits"%ROWTYPE;
    it JSONB;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' THEN
        RETURN;
    END IF;

    v_pipeline := map_pipeline(_src->'object_attributes', _webhook_event_id);
    v_merge_request := map_merge_request(_src->'merge_request', _webhook_event_id);
    v_user := map_user(_src->'user', _webhook_event_id);
    v_project := map_project(_src->'project', _webhook_event_id);
    v_commit := map_commit(_src->'commit', _webhook_event_id);

    IF v_pipeline IS NULL THEN
        RETURN;
    END IF;

    IF (_webhook_event_id IS NOT NULL AND v_pipeline."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        -- Only allow backfilling build entities, even if this webhook doesn't have the latest data.
    ELSE
        -- Only update these feilds if we have the latest information.
        UPDATE "Pipelines" SET
            "MergeRequestId" = COALESCE(v_merge_request."Id", v_pipeline."MergeRequestId"),
            "UserId" = COALESCE(v_user."Id", v_pipeline."UserId"),
            "ProjectId" = COALESCE(v_project."Id", v_pipeline."ProjectId"),
            "CommitId" = COALESCE(v_commit."Id", v_pipeline."CommitId")
        WHERE "Id" = v_pipeline."Id";
    END IF;

    -- Import and apply builds.
    PERFORM map_build(build_json, _webhook_event_id, _webhook_received_at, v_pipeline."Id") FROM jsonb_array_elements(_src->'builds') AS build_json;

    RETURN NEXT v_pipeline;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';