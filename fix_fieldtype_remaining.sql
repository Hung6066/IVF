-- Fix remaining fields with FieldType = 0
-- This updates any remaining fields that weren't caught by the first script

-- First, let's see what's left
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    "OptionsJson"
FROM "FormFields"
WHERE "FieldType" = 0;

-- Update all remaining FieldType = 0 to Checkbox (11) if they have no options
UPDATE "FormFields"
SET "FieldType" = 11
WHERE "FieldType" = 0 
  AND ("OptionsJson" IS NULL OR "OptionsJson" = '' OR "OptionsJson" = '[]');

-- Update all remaining FieldType = 0 to Text (1) as final fallback
UPDATE "FormFields"
SET "FieldType" = 1
WHERE "FieldType" = 0;

-- Verify no more 0 values
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    CASE "FieldType"
        WHEN 1 THEN 'Text'
        WHEN 2 THEN 'TextArea'
        WHEN 3 THEN 'Number'
        WHEN 8 THEN 'Dropdown'
        WHEN 10 THEN 'Radio'
        WHEN 11 THEN 'Checkbox'
        WHEN 12 THEN 'FileUpload'
        WHEN 16 THEN 'Tags'
        ELSE 'Unknown (' || "FieldType" || ')'
    END AS "FieldTypeName"
FROM "FormFields"
ORDER BY "CreatedAt" DESC;
