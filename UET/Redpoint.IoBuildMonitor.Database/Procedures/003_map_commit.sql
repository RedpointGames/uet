DROP FUNCTION IF EXISTS map_commit;
CREATE FUNCTION map_commit (IN _src jsonb, IN _webhook_event_id BIGINT) RETURNS SETOF "Commits" AS
$BODY$
DECLARE 
    existing_row "Commits"%ROWTYPE;
    selected_count INT;
    v_id TEXT;
BEGIN
    IF _src IS NULL OR jsonb_typeof(_src) = 'null' OR jsonb_typeof(_src->'id') = 'null' OR COALESCE(TRIM(_src->'id'->>0), '') = '' THEN
        RETURN;
    END IF;

    v_id := (_src->'id')->>0;

    SELECT * INTO existing_row FROM "Commits" WHERE "Id" = v_id;
    GET DIAGNOSTICS selected_count = ROW_COUNT;
    IF (selected_count > 0 AND _webhook_event_id IS NOT NULL AND existing_row."LastUpdatedByWebhookEventId" > _webhook_event_id) THEN
        RETURN NEXT existing_row;
        RETURN;
    END IF;
    
    INSERT INTO "Commits"
    (
        "Id",
        "Message",
        "Timestamp",
        "Url",
        "AuthorName",
        "AuthorEmail",
        "LastUpdatedByWebhookEventId"
    )
    VALUES
    (
        v_id,
        (_src->'message')->>0,
        (_src->'timestamp')->>0,
        (_src->'url')->>0,
        (_src->'author'->'name')->>0,
        (_src->'author'->'email')->>0,
        COALESCE(_webhook_event_id, 0)
    )
    ON CONFLICT ("Id") DO UPDATE SET
        "Message" = EXCLUDED."Message",
        "Timestamp" = EXCLUDED."Timestamp",
        "Url" = EXCLUDED."Url",
        "AuthorName" = EXCLUDED."AuthorName",
        "AuthorEmail" = EXCLUDED."AuthorEmail",
        "LastUpdatedByWebhookEventId" = GREATEST(EXCLUDED."LastUpdatedByWebhookEventId", "Commits"."LastUpdatedByWebhookEventId")
    WHERE "Commits"."Id" = EXCLUDED."Id";
    SELECT * INTO existing_row FROM "Commits" WHERE "Id" = v_id;
    
    RETURN NEXT existing_row;
    RETURN;
END
$BODY$
LANGUAGE 'plpgsql';