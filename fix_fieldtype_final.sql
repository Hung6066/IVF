-- Targeted fix for specific fields that got set to wrong types
-- This will update based on the actual field labels shown in the console

-- First, check current state
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    "OptionsJson",
    CASE "FieldType"
        WHEN 1 THEN 'Text'
        WHEN 2 THEN 'TextArea'
        WHEN 8 THEN 'Dropdown'
        WHEN 10 THEN 'Radio'
        WHEN 11 THEN 'Checkbox'
        WHEN 16 THEN 'Tags'
        ELSE 'Unknown (' || "FieldType" || ')'
    END AS "CurrentTypeName"
FROM "FormFields"
ORDER BY "DisplayOrder";

-- Update Tags field (label = 'Tags')
UPDATE "FormFields"
SET "FieldType" = 16
WHERE "Label" = 'Tags' OR "FieldKey" LIKE '%tag%';

-- Update Checkbox field (label = 'Checkbox')
UPDATE "FormFields"
SET "FieldType" = 11
WHERE "Label" = 'Checkbox' OR "FieldKey" LIKE '%checkbox%';

-- Verify the updates
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    CASE "FieldType"
        WHEN 1 THEN 'Text'
        WHEN 2 THEN 'TextArea'
        WHEN 8 THEN 'Dropdown'
        WHEN 10 THEN 'Radio'
        WHEN 11 THEN 'Checkbox'
        WHEN 16 THEN 'Tags'
        ELSE 'Unknown (' || "FieldType" || ')'
    END AS "FieldTypeName",
    "OptionsJson"
FROM "FormFields"
ORDER BY "DisplayOrder";
