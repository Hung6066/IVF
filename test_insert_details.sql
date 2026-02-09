-- Test inserting a FormFieldValueDetail manually to verify table and constraints work

-- First, get a recent FormFieldValue ID to use as foreign key
SELECT 
    fv."Id" as "FieldValueId",
    fv."FormFieldId",
    fv."FormResponseId",
    fv."TextValue",
    fv."JsonValue"
FROM "form_field_values" fv
ORDER BY fv."CreatedAt" DESC
LIMIT 5;

-- Insert a test detail (replace the GUID with an actual FieldValueId from above)
-- INSERT INTO "FormFieldValueDetails" ("Id", "FormFieldValueId", "Value", "Label", "ConceptId", "CreatedAt", "IsDeleted")
-- VALUES (
--     gen_random_uuid(),
--     'REPLACE_WITH_ACTUAL_FIELD_VALUE_ID',
--     'test_value',
--     'Test Label',
--     NULL,
--     NOW(),
--     false
-- );

-- Check if the insert worked
SELECT COUNT(*) as "TotalDetails" FROM "FormFieldValueDetails";

-- View all details with their parent field values
SELECT 
    fvd."Id",
    fvd."Value",
    fvd."Label",
    fvd."ConceptId",
    fv."FormFieldId",
    fv."TextValue"
FROM "FormFieldValueDetails" fvd
JOIN "form_field_values" fv ON fv."Id" = fvd."FormFieldValueId"
ORDER BY fvd."CreatedAt" DESC
LIMIT 10;
