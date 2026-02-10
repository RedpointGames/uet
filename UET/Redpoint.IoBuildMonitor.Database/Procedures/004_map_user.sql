DROP FUNCTION IF EXISTS map_user;
CREATE FUNCTION map_user (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Users" AS
$BODY$
DECLARE 
    existing_row "Users"%ROWTYPE;
    selected_count INT;
    v_id BIGINT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')::BIGINT;

    SELECT * INTO existing_row FROM "Users" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;
    
    INSERT INTO "Users"
    (
        "Id",
        "Name",
        "Username",
        "AvatarUrl",
        "Email",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'name')->>0,
        (_src->'username')->>0,
        (_src->'avatar_url')->>0,
        (_src->'email')->>0,
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Name" = EXCLUDED."Name",
        "Username" = EXCLUDED."Username",
        "AvatarUrl" = EXCLUDED."AvatarUrl",
        "Email" = EXCLUDED."Email",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Users"."LastUpdatedByWebhookEventId")
    WHERE "Users"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Users" WHERE "Id" = v_id;
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';