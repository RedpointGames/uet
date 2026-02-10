DROP FUNCTION IF EXISTS map_project;
CREATE FUNCTION map_project (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Projects" AS
$BODY$
DECLARE 
    existing_row "Projects"%ROWTYPE;
    selected_count INT;
    v_id BIGINT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    SELECT * INTO existing_row FROM "Projects" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;

    INSERT INTO "Projects"
    (
        "Id",
        "Name",
        "Description",
        "WebUrl",
        "AvatarUrl",
        "GitSshUrl",
        "GitHttpUrl",
        "Namespace",
        "VisibilityLevel",
        "PathWithNamespace",
        "DefaultBranch",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'name')->>0,
        (_src->'description')->>0,
        (_src->'web_url')->>0,
        (_src->'avatar_url')->>0,
        (_src->'git_ssh_url')->>0,
        (_src->'git_http_url')->>0,
        (_src->'namespace')->>0,
        cast_to_nullable_bigint(_src->'visibility_level'),
        (_src->'path_with_namespace')->>0,
        (_src->'default_branch')->>0,
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Name" = EXCLUDED."Name",
        "Description" = EXCLUDED."Description",
        "WebUrl" = EXCLUDED."WebUrl",
        "AvatarUrl" = EXCLUDED."AvatarUrl",
        "GitSshUrl" = EXCLUDED."GitSshUrl",
        "GitHttpUrl" = EXCLUDED."GitHttpUrl",
        "Namespace" = EXCLUDED."Namespace",
        "VisibilityLevel" = EXCLUDED."VisibilityLevel",
        "PathWithNamespace" = EXCLUDED."PathWithNamespace",
        "DefaultBranch" = EXCLUDED."DefaultBranch",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Projects"."LastUpdatedByWebhookEventId")
    WHERE "Projects"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Projects" WHERE "Id" = v_id;
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';