-- Check current FieldType values
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    "OptionsJson"
FROM "FormFields"
ORDER BY "CreatedAt" DESC;

-- Update FieldType based on field characteristics
-- This is a one-time fix for existing data

-- Radio fields (have options, label contains "radio" or "nút")
UPDATE "FormFields"
SET "FieldType" = 10
WHERE "FieldType" = 0 
  AND ("OptionsJson" IS NOT NULL AND "OptionsJson" != '[]')
  AND (LOWER("Label") LIKE '%radio%' OR LOWER("Label") LIKE '%nút%');

-- Checkbox fields (label contains "checkbox")
UPDATE "FormFields"
SET "FieldType" = 11
WHERE "FieldType" = 0 
  AND (LOWER("Label") LIKE '%checkbox%');

-- Dropdown fields (have options, label contains "dropdown" or "chọn")
UPDATE "FormFields"
SET "FieldType" = 8
WHERE "FieldType" = 0 
  AND ("OptionsJson" IS NOT NULL AND "OptionsJson" != '[]')
  AND (LOWER("Label") LIKE '%dropdown%' OR LOWER("Label") LIKE '%danh sách%');

-- Tags fields (label contains "tag" or "tags")
UPDATE "FormFields"
SET "FieldType" = 16
WHERE "FieldType" = 0 
  AND (LOWER("Label") LIKE '%tag%');

-- File upload fields
UPDATE "FormFields"
SET "FieldType" = 12
WHERE "FieldType" = 0 
  AND (LOWER("Label") LIKE '%file%' OR LOWER("Label") LIKE '%tải%');

-- Default remaining 0 values to Text (1)
UPDATE "FormFields"
SET "FieldType" = 1
WHERE "FieldType" = 0;

-- Verify the update
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
        ELSE 'Unknown'
    END AS "FieldTypeName"
FROM "FormFields"
ORDER BY "CreatedAt" DESC;
