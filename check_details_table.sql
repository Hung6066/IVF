-- Check if FormFieldValueDetails table exists and view its structure
SELECT 
    table_name,
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'FormFieldValueDetails'
ORDER BY ordinal_position;

-- Check if there are any records in the table
SELECT COUNT(*) as "TotalRecords"
FROM "FormFieldValueDetails";

-- Check recent form responses and their field values with details
SELECT 
    fr."Id" as "ResponseId",
    fr."SubmittedAt",
    fv."Id" as "FieldValueId",
    fv."FormFieldId",
    fv."TextValue",
    fv."JsonValue",
    COUNT(fvd."Id") as "DetailCount"
FROM "FormResponses" fr
JOIN "FormFieldValues" fv ON fv."FormResponseId" = fr."Id"
LEFT JOIN "FormFieldValueDetails" fvd ON fvd."FormFieldValueId" = fv."Id"
WHERE fr."SubmittedAt" > NOW() - INTERVAL '1 day'
GROUP BY fr."Id", fr."SubmittedAt", fv."Id", fv."FormFieldId", fv."TextValue", fv."JsonValue"
ORDER BY fr."SubmittedAt" DESC
LIMIT 20;

-- Check if any details exist at all
SELECT 
    fvd."Id",
    fvd."FormFieldValueId",
    fvd."Value",
    fvd."Label",
    fvd."ConceptId",
    fv."FormFieldId"
FROM "FormFieldValueDetails" fvd
JOIN "FormFieldValues" fv ON fv."Id" = fvd."FormFieldValueId"
LIMIT 10;
