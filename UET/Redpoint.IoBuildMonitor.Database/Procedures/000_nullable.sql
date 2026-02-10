DROP FUNCTION IF EXISTS cast_to_nullable_bigint;
CREATE FUNCTION cast_to_nullable_bigint (v JSONB) RETURNS BIGINT AS 
$BODY$
BEGIN
    RETURN CASE
        WHEN jsonb_typeof(v) = 'null' THEN NULL
        ELSE v::BIGINT
    END;
END
$BODY$
LANGUAGE 'plpgsql';

DROP FUNCTION IF EXISTS cast_to_nullable_boolean;
CREATE FUNCTION cast_to_nullable_boolean (v JSONB) RETURNS BOOLEAN AS 
$BODY$
BEGIN
    RETURN CASE
        WHEN jsonb_typeof(v) = 'null' THEN NULL
        ELSE v::BOOLEAN
    END;
END
$BODY$
LANGUAGE 'plpgsql';