DROP FUNCTION IF EXISTS map_build;
CREATE FUNCTION map_build (IN _src jsonb, IN _webhook_event_id BIGINT, IN _webhook_received_at TIMESTAMPTZ, IN _pipeline_id BIGINT) RETURNS SETOF "Builds" AS
$BODY$
DECLARE 
    v_id BIGINT;
    v_build "Builds"%ROWTYPE;
    v_user "Users"%ROWTYPE;
    v_runner "Runners"%ROWTYPE;
    v_runner_existing_id BIGINT;
    selected_count INT;
    emit_change BOOLEAN;
    emit_old_status TEXT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    -- Do we have an existing row that is up-to-date?
    SELECT * INTO v_build FROM "Builds" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND v_build."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT v_build;
        RETURN;
    END IF;

    IF v_build IS NOT NULL THEN
        -- Check if permit updating this build.
        IF v_build."Status" = 'created' THEN
            -- Always allow moving from the "created" status.
        ELSEIF v_build."Status" = 'pending' THEN
            IF (_src->'status')->>0 = 'created' THEN
                -- This is a stale event. Ignore it.
                RETURN NEXT v_build;
                RETURN;
            END IF;
        ELSEIF v_build."Status" = 'running' THEN
            IF (_src->'status')->>0 = 'created' OR (_src->'status')->>0 = 'pending' THEN
                -- This is a stale event. Ignore it.
                RETURN NEXT v_build;
                RETURN;
            END IF;
        ELSEIF v_build."Status" = 'manual' THEN
            -- Always allow moving from the "manual" status.
        ELSEIF v_build."Status" = 'success' OR
                v_build."Status" = 'failed' OR
                v_build."Status" = 'skipped' OR
                v_build."Status" = 'canceled' THEN
            IF v_build."Status" <> (_src->'status')->>0 THEN
                -- This is a stale event. Ignore it.
                RETURN NEXT v_build;
                RETURN;
            END IF;
        END IF;
        IF jsonb_typeof(_src->'started_at') = 'null' AND v_build."StartedAt" IS NOT NULL THEN
            -- If the source doesn't have a started at, but we've already received it, then
            -- this is a stale event.
            RETURN NEXT v_build;
            RETURN;
        END IF;
        IF jsonb_typeof(_src->'finished_at') = 'null' AND v_build."FinishedAt" IS NOT NULL THEN
            -- If the source doesn't have a started at, but we've already received it, then
            -- this is a stale event.
            RETURN NEXT v_build;
            RETURN;
        END IF;
    END IF;

    -- If the build status is changing, generate an event.
    emit_change := FALSE;
    IF v_build IS NULL OR v_build."Status" <> (_src->'status')->>0 THEN
        emit_old_status := v_build."Status";
        emit_change := TRUE;
    END IF;

    -- Insert new build or update the existing one.
    v_user := map_user(_src->'user', _webhook_event_id);    
    v_runner := NULL;
    IF jsonb_typeof(_src->'runner') <> 'null' AND 
        _src->'status'->>0 <> 'success' AND 
        _src->'status'->>0 <> 'failed' AND 
        _src->'status'->>0 <> 'canceled' AND 
        _src->'status'->>0 <> 'manual' AND 
        _src->'status'->>0 <> 'skipped' THEN
        v_runner := map_runner(_src->'runner', _webhook_event_id);
    END IF;
    v_runner_existing_id := v_build."RunnerId";
    INSERT INTO "Builds"
    (
        "Id",
        "Stage",
        "Name",
        "Status",
        "CreatedAt",
        "StartedAt",
        "FinishedAt",
        "Duration",
        "When",
        "Manual",
        "AllowFailure",
        "UserId",
        "PipelineId",
        "ArtifactsFilename",
        "ArtifactsSize",
        -- "DownstreamPipelineId",
        "RunnerId",
        "RanWithTags",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'stage')->>0,
        (_src->'name')->>0,
        (_src->'status')->>0,
        date_gitlab_from_string((_src->'created_at')->>0),
        date_gitlab_from_string((_src->'started_at')->>0),
        date_gitlab_from_string((_src->'finished_at')->>0),
        cast_to_nullable_bigint(_src->'duration'),
        (_src->'when')->>0,
        cast_to_nullable_boolean(_src->'manual'),
        cast_to_nullable_boolean(_src->'allow_failure'),
        v_user."Id",
        _pipeline_id,
        (_src->'artifacts_file'->'filename')->>0,
        cast_to_nullable_bigint(_src->'artifacts_file'->'size'),
        -- 
        v_runner."Id",
        COALESCE(v_runner."Tags", array[]::text[]),
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Stage" = EXCLUDED."Stage",
        "Name" = EXCLUDED."Name",
        "Status" = EXCLUDED."Status",
        "CreatedAt" = EXCLUDED."CreatedAt",
        "StartedAt" = EXCLUDED."StartedAt",
        "FinishedAt" = EXCLUDED."FinishedAt",
        "Duration" = EXCLUDED."Duration",
        "When" = EXCLUDED."When",
        "Manual" = EXCLUDED."Manual",
        "AllowFailure" = EXCLUDED."AllowFailure",
        "UserId" = COALESCE(EXCLUDED."UserId", "Builds"."UserId"),
        "PipelineId" = COALESCE(EXCLUDED."PipelineId", "Builds"."PipelineId"),
        "ArtifactsFilename" = EXCLUDED."ArtifactsFilename",
        "ArtifactsSize" = EXCLUDED."ArtifactsSize",
        "RunnerId" = v_runner."Id",
        "RanWithTags" = COALESCE(v_runner."Tags", "Builds"."RanWithTags", array[]::text[]),
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Builds"."LastUpdatedByWebhookEventId")
    WHERE "Builds"."Id" = EXCLUDED."Id";
    SELECT * INTO v_build FROM "Builds" WHERE "Id" = v_id;

    -- Add the build status change if we need to.
    IF emit_change THEN
        INSERT INTO "BuildStatusChanges"
        (
            "BuildId",
            "StatusChangedAt",
            "OldStatus",
            "NewStatus"
        )
        VALUES
        (
            v_id,
            _webhook_received_at,
            emit_old_status,
            (_src->'status')->>0
        );
    END IF;
    
    -- Generate utilization invalidation if needed.
    IF v_build."RunnerId" IS NOT NULL AND v_build."RunnerId" <> v_runner_existing_id THEN
        INSERT INTO "UtilizationInvalidation"
        (
            "Timestamp"
        )
        VALUES
        (
            DATE_TRUNC('minute', v_build."CreatedAt")
        );
    END IF;

    -- Generate utilization block if needed.
    IF v_runner_existing_id IS NOT NULL AND 
        v_build."RunnerId" IS NULL AND 
        v_build."StartedAt" IS NOT NULL AND 
        v_build."FinishedAt" IS NOT NULL THEN
        PERFORM apply_utilization_period(v_build."StartedAt", v_build."FinishedAt", v_runner_existing_id);
    END IF;

    RETURN NEXT v_build;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';